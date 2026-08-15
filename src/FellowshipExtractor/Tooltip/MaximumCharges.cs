using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

public sealed record ChargeCounts(double? Charges, IReadOnlyDictionary<string, double>? ByLevelType)
{
    public static readonly ChargeCounts None = new(null, null);
}

public static class MaximumCharges
{
    private const string Function = "GetMaximumCharges";

    public static ChargeCounts Of(UObject cdo, ConstantsTable constants, TypeMappings mappings)
    {
        if (Compiled(cdo) is not { ScriptBytecode: { Length: > 0 } bytecode } body) return ChargeCounts.None;

        var walk = new Walk(cdo, constants, mappings, body, bytecode);

        return new ChargeCounts(walk.Answer(), walk.ByLevelType);
    }

    private static UStruct? Compiled(UObject cdo)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(Function, StringComparison.Ordinal)) return body;
        }

        return null;
    }

    private sealed class Walk(
        UObject cdo,
        ConstantsTable constants,
        TypeMappings mappings,
        UStruct function,
        KismetExpression[] bytecode)
    {
        private const string ScalableRead = "GetScalableFloat";
        private const string ValidityCheck = "IsValid";
        private const string TagCheck = "HasMatchingGameplayTag";
        private const string Choice = "SelectInt";
        private const string HalfUp = "Round";
        private const string TowardZero = "FTrunc";
        private const string RowHandle = "CurveTableRowHandle";
        private const string ValueField = "Value";
        private const string CurveField = "Curve";
        private const string RowField = "RowName";
        private const string NoRow = "None";
        private const int Budget = 512;
        private const int MaxDepth = 24;

        private readonly Dictionary<string, KismetExpression> _locals = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, double>? ByLevelType { get; private set; }

        public double? Answer()
        {
            var at = 0;

            for (var step = 0; step < Budget && at < bytecode.Length; step++)
            {
                switch (bytecode[at])
                {
                    case EX_Return returned:
                        return Number(returned.ReturnExpression) is { } charges
                            ? charges
                            : ByLevelType is not null
                                ? null
                                : throw Unreadable($"returns {Shape(returned.ReturnExpression)}, which is no number");

                    case EX_JumpIfNot branch:
                        at = Truth(branch.BooleanExpression) switch
                        {
                            false => At(branch.CodeOffset),
                            true => at + 1,
                            null => throw Unreadable(
                                $"branches on {Shape(branch.BooleanExpression)}, which is neither true nor false " +
                                "for a character holding no talents"),
                        };
                        continue;

                    case EX_Jump jump:
                        at = At(jump.CodeOffset);
                        continue;

                    case EX_EndOfScript:
                        throw Unreadable("ends without returning a charge count");

                    default:
                        if (!Bind(bytecode[at]))
                        {
                            throw Unreadable($"runs a {bytecode[at].Token} statement this reader does not evaluate");
                        }

                        at++;
                        continue;
                }
            }

            throw Unreadable($"does not return within {Budget} statements");
        }

        private bool Bind(KismetExpression statement)
        {
            var assignment = statement switch
            {
                EX_Let let => (Variable: let.Variable, Value: let.Assignment),
                EX_LetObj let => (Variable: let.Variable, Value: let.Assignment),
                EX_LetBool let => (Variable: let.Variable, Value: let.Assignment),
                _ => default,
            };

            if (assignment.Variable is null || assignment.Value is null) return false;
            if (DynamicTextFloats.Name(assignment.Variable) is not { } name) return false;

            _locals[name] = assignment.Value;
            return true;
        }

        private int At(uint offset)
        {
            for (var index = 0; index < bytecode.Length; index++)
            {
                if (bytecode[index].StatementIndex == (int)offset) return index;
            }

            throw Unreadable(
                $"jumps to code offset {offset}, which starts none of its statements " +
                $"({string.Join(", ", bytecode.Select(statement => statement.StatementIndex))})");
        }

        private KismetExpression? Bound(KismetExpression? expression) =>
            DynamicTextFloats.Name(expression) is { } name && _locals.TryGetValue(name, out var assigned)
                ? assigned
                : null;

        private bool? Truth(KismetExpression? expression, int depth = 0)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_True: return true;
                case EX_False: return false;
                case EX_DynamicCast: return true;
                case EX_Cast cast: return Truth(cast.Target, depth + 1);

                case EX_CallMath call when call.StackNode.Name.Equals(ValidityCheck, StringComparison.Ordinal):
                    return true;

                case EX_LocalVirtualFunction call
                    when call.VirtualFunctionName.Text.Equals(TagCheck, StringComparison.Ordinal):
                    return false;

                case EX_VirtualFunction call
                    when call.VirtualFunctionName.Text.Equals(TagCheck, StringComparison.Ordinal):
                    return false;

                case EX_Context context: return Truth(context.ContextExpression, depth + 1);
            }

            return Bound(expression) is { } assigned ? Truth(assigned, depth + 1) : null;
        }

        private double? Number(KismetExpression? expression, int depth = 0)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_IntConst value: return value.Value;
                case EX_Int64Const value: return value.Value;
                case EX_ByteConst value: return value.Value;
                case EX_IntConstByte value: return value.Value;
                case EX_FloatConst value: return value.Value;
                case EX_DoubleConst value: return value.Value;
                case EX_IntOne: return 1;
                case EX_IntZero: return 0;

                case EX_Cast cast: return Number(cast.Target, depth + 1);
                case EX_CallMath call: return Called(call, depth);
            }

            return Bound(expression) is { } assigned ? Number(assigned, depth + 1) : null;
        }

        private double? Called(EX_CallMath call, int depth)
        {
            var function = call.StackNode.Name;

            if (function.StartsWith(ScalableRead, StringComparison.Ordinal))
            {
                return Scalable(call.Parameters.FirstOrDefault(), depth + 1);
            }

            if (function.Equals(HalfUp, StringComparison.Ordinal))
            {
                return Number(call.Parameters.FirstOrDefault(), depth + 1) is { } value
                    ? Math.Floor(value + 0.5)
                    : null;
            }

            if (function.Equals(TowardZero, StringComparison.Ordinal))
            {
                return Number(call.Parameters.FirstOrDefault(), depth + 1) is { } value
                    ? Math.Truncate(value)
                    : null;
            }

            if (function.Equals(Choice, StringComparison.Ordinal))
            {
                return Truth(call.Parameters.ElementAtOrDefault(2)) switch
                {
                    true => Number(call.Parameters.ElementAtOrDefault(0), depth + 1),
                    false => Number(call.Parameters.ElementAtOrDefault(1), depth + 1),
                    null => throw Unreadable(
                        $"picks between two charge counts on {Shape(call.Parameters.ElementAtOrDefault(2))}, " +
                        "which is neither true nor false for a character holding no talents"),
                };
            }

            return null;
        }

        private double? Scalable(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;
            if (expression is EX_SwitchValue chosen) return Switched(chosen, depth);

            if (Struct(expression, depth) is not { } scalable) return null;

            var (multiplier, row) = scalable;
            if (row is null) return multiplier;

            if (constants.Lookup(row).Record?.Value is not { } value)
            {
                throw Unreadable(
                    $"reads its charge count off row {row}, which no ability constants table gives a number for");
            }

            return multiplier * value;
        }

        private double? Switched(EX_SwitchValue chosen, int depth)
        {
            var byOrdinal = new Dictionary<long, double>();

            foreach (var branch in chosen.Cases)
            {
                var value = Scalable(branch.CaseTerm, depth + 1) ?? throw Unreadable(
                    $"chooses its charge count on {Shape(chosen.IndexTerm)}, and the branch for " +
                    $"{Shape(branch.CaseIndexValueTerm)} reads no number");

                if (Number(branch.CaseIndexValueTerm) is not { } ordinal)
                {
                    throw Unreadable(
                        $"chooses its charge count on {Shape(chosen.IndexTerm)} against " +
                        $"{Shape(branch.CaseIndexValueTerm)}, which is no ordinal");
                }

                byOrdinal[(long)ordinal] = value;
            }

            var distinct = new SortedSet<double>(byOrdinal.Values);
            if (Scalable(chosen.DefaultTerm, depth + 1) is { } fallback) distinct.Add(fallback);

            if (distinct.Count == 1) return distinct.Min;

            ByLevelType = Keyed(chosen, byOrdinal);
            return null;
        }

        private IReadOnlyDictionary<string, double> Keyed(
            EX_SwitchValue chosen, IReadOnlyDictionary<long, double> byOrdinal)
        {
            var enumerated = Enumerated(chosen.IndexTerm) ?? throw Unreadable(
                $"answers a different charge count per {Shape(chosen.IndexTerm)}, which names no enum, so the " +
                "counts cannot be keyed by anything the export states");

            var members = EnumOrdinals.Members(mappings, enumerated);
            var named = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (ordinal, value) in byOrdinal)
            {
                if (!members.TryGetValue(ordinal, out var member))
                {
                    throw Unreadable($"answers a charge count for {enumerated} {ordinal}, which that enum has no member for");
                }

                named[member] = value;
            }

            return named;
        }

        private string? Enumerated(KismetExpression? index)
        {
            if (DynamicTextFloats.Name(index) is not { } name) return null;

            foreach (var child in function.ChildProperties ?? [])
            {
                if (!child.Name.Text.Equals(name, StringComparison.Ordinal)) continue;

                return child switch
                {
                    FEnumProperty { Enum.Name: { Length: > 0 } named } => named,
                    FByteProperty { Enum.Name: { Length: > 0 } named } => named,
                    _ => null,
                };
            }

            return null;
        }

        private (double Multiplier, string? Row)? Struct(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;
            if (expression is EX_StructConst constant) return Inline(constant);
            if (Bound(expression) is { } assigned) return Struct(assigned, depth + 1);
            if (DynamicTextFloats.Name(expression) is not { } property) return null;
            if (PropertyWalk.GetStruct(cdo, property) is not { } scalable) return null;

            var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve
                ? PropertyWalk.GetString(curve, RowField)
                : null;

            return (PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1, Named(row));
        }

        private static (double Multiplier, string? Row)? Inline(EX_StructConst constant)
        {
            double? multiplier = null;
            string? row = null;

            foreach (var member in constant.Properties)
            {
                switch (member)
                {
                    case EX_FloatConst value when multiplier is null:
                        multiplier = value.Value;
                        break;

                    case EX_DoubleConst value when multiplier is null:
                        multiplier = value.Value;
                        break;

                    case EX_StructConst handle when handle.Struct.Name.Equals(RowHandle, StringComparison.Ordinal):
                        row = handle.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text;
                        break;
                }
            }

            return (multiplier ?? 1, Named(row));
        }

        private static string? Named(string? row) =>
            row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal) ? null : row;

        private string Shape(KismetExpression? expression, int depth = 0)
        {
            if (expression is null) return "nothing";
            if (depth <= MaxDepth && Bound(expression) is { } assigned) return Shape(assigned, depth + 1);

            return expression switch
            {
                EX_Context context => Shape(context.ContextExpression, depth + 1),
                EX_CallMath call => call.StackNode.Name,
                EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
                EX_VirtualFunction call => call.VirtualFunctionName.Text,
                EX_FinalFunction call => call.StackNode.Name,
                EX_ByteConst value => value.Value.ToString(),
                EX_IntConst value => value.Value.ToString(),
                _ => DynamicTextFloats.Name(expression) ?? expression.Token.ToString(),
            };
        }

        private InvalidOperationException Unreadable(string what) =>
            new($"{cdo.Owner?.Name ?? cdo.Name}.{Function} {what}. The charge count a tooltip prints is what that " +
                "function returns for a character holding no talents, and it cannot be guessed from a row name.");
    }
}
