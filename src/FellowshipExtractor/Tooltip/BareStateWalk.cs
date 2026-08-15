using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Tooltip;

public sealed class BareStateWalk(
    UStruct function, UObject cdo, ConstantsTable constants, TypeMappings? mappings)
{
    private const string ScalableRead = "GetScalableFloat";
    private const string RowHandle = "CurveTableRowHandle";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string NoRow = "None";
    private const string TagStruct = "GameplayTag";
    private const string TagNameField = "TagName";
    private const int Budget = 1024;
    private const int MaxDepth = 32;

    private readonly KismetExpression[] _bytecode = function.ScriptBytecode ?? [];
    private readonly Dictionary<string, KismetExpression> _locals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _returned = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gates = new(StringComparer.Ordinal);

    public static UStruct? Compiled(UObject cdo, string function)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(function, StringComparison.Ordinal)) return body;
        }

        return null;
    }

    public Func<string, InvalidOperationException>? Unreadable { get; init; }

    private bool Inlined { get; init; }

    public IReadOnlySet<string> AssumedTags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> Gates => _gates;

    public (double Low, double High)? Range { get; private set; }

    public IReadOnlyDictionary<string, double>? ByLevelType { get; private set; }

    public IEnumerable<KismetExpression> Reached()
    {
        var at = 0;

        for (var step = 0; step < Budget && at < _bytecode.Length; step++)
        {
            var statement = _bytecode[at];

            switch (statement)
            {
                case EX_Return or EX_EndOfScript:
                    yield break;

                case EX_JumpIfNot branch:
                    if (Truth(branch.BooleanExpression) is not { } taken) yield break;

                    at = taken ? at + 1 : At(branch.CodeOffset);
                    if (at < 0) yield break;
                    continue;

                case EX_Jump jump:
                    at = At(jump.CodeOffset);
                    if (at < 0) yield break;
                    continue;

                default:
                    yield return statement;

                    Bind(statement);
                    Inline(statement);
                    at++;
                    continue;
            }
        }
    }

    private void Inline(KismetExpression statement)
    {
        if (Inlined) return;
        if (statement is not EX_LocalVirtualFunction call) return;
        if (Compiled(cdo, call.VirtualFunctionName.Text) is not { ScriptBytecode.Length: > 0 } body) return;

        var inside = new BareStateWalk(body, cdo, constants, mappings)
        {
            Inlined = true,
            AssumedTags = AssumedTags,
        };

        foreach (var _ in inside.Reached()) { }

        foreach (var gate in inside._gates) _gates.Add(gate);

        var at = 0;

        foreach (var child in body.ChildProperties ?? [])
        {
            if (child is not FProperty parameter) continue;
            if (!parameter.PropertyFlags.HasFlag(EPropertyFlags.Parm)) continue;
            if (parameter.PropertyFlags.HasFlag(EPropertyFlags.ReturnParm)) continue;

            if (parameter.PropertyFlags.HasFlag(EPropertyFlags.OutParm) &&
                DynamicTextFloats.Name(call.Parameters.ElementAtOrDefault(at)) is { } local &&
                inside._locals.TryGetValue(parameter.Name.Text, out var written) &&
                inside.Number(written) is { } answered)
            {
                _returned[local] = answered;
            }

            at++;
        }
    }

    public void BindEach()
    {
        foreach (var statement in _bytecode) Bind(statement);
    }

    public double? Answer()
    {
        var at = 0;

        for (var step = 0; step < Budget && at < _bytecode.Length; step++)
        {
            switch (_bytecode[at])
            {
                case EX_Return returned:
                    if (Number(returned.ReturnExpression) is { } answer) return answer;
                    if (ByLevelType is not null || Range is not null) return null;

                    return Refuse($"returns {Shape(returned.ReturnExpression)}, which is no number");

                case EX_JumpIfNot branch:
                    if (Truth(branch.BooleanExpression) is not { } taken)
                    {
                        return Refuse(
                            $"branches on {Shape(branch.BooleanExpression)}, which is neither " +
                            "true nor false for a character holding no talents");
                    }

                    at = taken ? at + 1 : At(branch.CodeOffset);
                    if (at < 0) return null;
                    continue;

                case EX_Jump jump:
                    at = At(jump.CodeOffset);
                    if (at < 0) return null;
                    continue;

                case EX_EndOfScript:
                    return Refuse("ends without returning a value");

                default:
                    if (!Bind(_bytecode[at]))
                    {
                        return Refuse(
                            $"runs a {_bytecode[at].Token} statement this reader does not evaluate");
                    }

                    at++;
                    continue;
            }
        }

        return Refuse($"does not return within {Budget} statements");
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
        for (var index = 0; index < _bytecode.Length; index++)
        {
            if (_bytecode[index].StatementIndex == (int)offset) return index;
        }

        Refuse($"jumps to code offset {offset}, which starts none of its statements " +
               $"({string.Join(", ", _bytecode.Select(statement => statement.StatementIndex))})");

        return -1;
    }

    private KismetExpression? Bound(KismetExpression? expression) =>
        DynamicTextFloats.Name(expression) is { } name && _locals.TryGetValue(name, out var assigned)
            ? assigned
            : null;

    public bool? Truth(KismetExpression? expression, int depth = 0)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_True: return true;
            case EX_False: return false;
            case EX_IntConst value: return value.Value != 0;
            case EX_ByteConst value: return value.Value != 0;
            case EX_DynamicCast: return true;
            case EX_Cast cast: return Truth(cast.Target, depth + 1);
            case EX_Context context: return Truth(context.ContextExpression, depth + 1);

            case EX_CallMath call: return CalledTruth(call, depth);

            case EX_LocalVirtualFunction call: return NamedTruth(call.VirtualFunctionName.Text, call.Parameters);
            case EX_VirtualFunction call: return NamedTruth(call.VirtualFunctionName.Text, call.Parameters);
            case EX_LocalFinalFunction call: return NamedTruth(call.StackNode.Name, call.Parameters);
            case EX_FinalFunction call: return NamedTruth(call.StackNode.Name, call.Parameters);
        }

        return Bound(expression) is { } assigned ? Truth(assigned, depth + 1) : null;
    }

    private static readonly string[] TagTests =
    [
        "HasMatchingGameplayTag",
        "HasAnyMatchingGameplayTags",
        "HasAllMatchingGameplayTags",
        "HasTag",
        "HasAnyTags",
        "HasAllTags",
    ];

    private bool? NamedTruth(string invoked, IReadOnlyList<KismetExpression> parameters)
    {
        if (!TagTests.Contains(invoked, StringComparer.Ordinal)) return null;

        var held = false;

        foreach (var parameter in parameters)
        {
            if (Tested(parameter) is not { } tag) continue;

            _gates.Add(tag);
            if (AssumedTags.Contains(tag)) held = true;
        }

        return held;
    }

    private string? Tested(KismetExpression? expression)
    {
        if (expression is EX_StructConst { Struct.Name: TagStruct } inline)
        {
            return Named(inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text);
        }

        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(cdo, property) is not { } held) return null;

        return Named(PropertyWalk.GetString(held, TagNameField));
    }

    private bool? CalledTruth(EX_CallMath call, int depth)
    {
        var invoked = call.StackNode.Name;

        if (invoked.Equals("IsValid", StringComparison.Ordinal)) return true;
        if (NamedTruth(invoked, call.Parameters) is { } known) return known;

        if (invoked.Equals("Not_PreBool", StringComparison.Ordinal))
        {
            return Truth(call.Parameters.FirstOrDefault(), depth + 1) is { } inner ? !inner : null;
        }

        if (invoked.Equals("BooleanAND", StringComparison.Ordinal))
        {
            var a = Truth(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Truth(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is false || b is false) return false;
            return a is true && b is true ? true : null;
        }

        if (invoked.Equals("BooleanOR", StringComparison.Ordinal))
        {
            var a = Truth(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Truth(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is true || b is true) return true;
            return a is false && b is false ? false : null;
        }

        var comparison = invoked.Split('_')[0];
        if (comparison is "Greater" or "GreaterEqual" or "Less" or "LessEqual" or "EqualEqual" or "NotEqual")
        {
            var a = Number(call.Parameters.ElementAtOrDefault(0), depth + 1);
            var b = Number(call.Parameters.ElementAtOrDefault(1), depth + 1);
            if (a is not { } left || b is not { } right) return null;

            return comparison switch
            {
                "Greater" => left > right,
                "GreaterEqual" => left >= right,
                "Less" => left < right,
                "LessEqual" => left <= right,
                "EqualEqual" => left == right,
                _ => left != right,
            };
        }

        return null;
    }

    public double? Number(KismetExpression? expression, int depth = 0)
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
            case EX_SwitchValue chosen: return Switched(chosen, depth);
        }

        if (DynamicTextFloats.Name(expression) is { } local && _returned.TryGetValue(local, out var written))
        {
            return written;
        }

        return Bound(expression) is { } assigned ? Number(assigned, depth + 1) : null;
    }

    private double? Called(EX_CallMath call, int depth)
    {
        var invoked = call.StackNode.Name;

        double? Argument(int index) => Number(call.Parameters.ElementAtOrDefault(index), depth + 1);

        if (invoked.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            return Scalable(call.Parameters.FirstOrDefault(), depth + 1);
        }

        if (invoked.StartsWith("Multiply_", StringComparison.Ordinal))
        {
            var a = Argument(0);
            var b = Argument(1);
            if (a == 0 || b == 0) return 0;
            return a is { } left && b is { } right ? left * right : null;
        }

        if (invoked.StartsWith("Add_", StringComparison.Ordinal))
        {
            return Argument(0) is { } left && Argument(1) is { } right ? left + right : null;
        }

        if (invoked.StartsWith("Subtract_", StringComparison.Ordinal))
        {
            return Argument(0) is { } left && Argument(1) is { } right ? left - right : null;
        }

        if (invoked.StartsWith("Divide_", StringComparison.Ordinal))
        {
            var divisor = Argument(1);
            if (divisor is 0) return null;
            if (Argument(0) is not { } dividend) return null;

            return dividend == 0 ? 0 : divisor is { } by ? dividend / by : null;
        }

        if (invoked is "RandomFloatInRange" or "RandomIntegerInRange")
        {
            if (Argument(0) is not { } least || Argument(1) is not { } most) return null;

            Range = (least, most);
            return null;
        }

        if (invoked is "SelectFloat" or "SelectInt" or "SelectDouble")
        {
            return Truth(call.Parameters.ElementAtOrDefault(2), depth + 1) switch
            {
                true => Argument(0),
                false => Argument(1),
                null => Refuse(
                    $"picks between two values on {Shape(call.Parameters.ElementAtOrDefault(2))}, " +
                    "which is neither true nor false for a character holding no talents"),
            };
        }

        if (invoked is "FClamp" or "Clamp")
        {
            return Argument(0) is { } value && Argument(1) is { } least && Argument(2) is { } most
                ? Math.Clamp(value, least, most)
                : null;
        }

        if (invoked is "FMax" or "Max")
        {
            return Argument(0) is { } left && Argument(1) is { } right ? Math.Max(left, right) : null;
        }

        if (invoked is "FMin" or "Min")
        {
            return Argument(0) is { } left && Argument(1) is { } right ? Math.Min(left, right) : null;
        }

        if (invoked.Equals("Round", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Floor(value + 0.5) : null;
        }

        if (invoked.Equals("FTrunc", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Truncate(value) : null;
        }

        if (invoked.Equals("FFloor", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Floor(value) : null;
        }

        if (invoked.Equals("FCeil", StringComparison.Ordinal))
        {
            return Argument(0) is { } value ? Math.Ceiling(value) : null;
        }

        if (invoked.Equals("Loge", StringComparison.Ordinal))
        {
            return Argument(0) is { } value and > 0 ? Math.Log(value) : null;
        }

        if (invoked.Equals("Log", StringComparison.Ordinal))
        {
            return Argument(0) is { } value and > 0 && Argument(1) is { } logBase and > 0 and not 1
                ? Math.Log(value) / Math.Log(logBase)
                : null;
        }

        if (invoked.StartsWith("Conv_", StringComparison.Ordinal) ||
            invoked.StartsWith("MakeLiteral", StringComparison.Ordinal))
        {
            return Argument(0);
        }

        return null;
    }

    private double? Switched(EX_SwitchValue chosen, int depth)
    {
        var answers = new SortedSet<double>();
        var cases = new List<(KismetExpression? Index, double Value)>();

        foreach (var branch in chosen.Cases)
        {
            if (Case(branch.CaseTerm, depth + 1) is not { } value)
            {
                return Refuse(
                    $"chooses its value on {Shape(chosen.IndexTerm)}, and the branch for " +
                    $"{Shape(branch.CaseIndexValueTerm)} reads no number");
            }

            cases.Add((branch.CaseIndexValueTerm, value));
            answers.Add(value);
        }

        if (Case(chosen.DefaultTerm, depth + 1) is { } fallback) answers.Add(fallback);
        if (answers.Count == 1) return answers.Min;

        var byOrdinal = new Dictionary<long, double>();

        foreach (var (index, value) in cases)
        {
            if (Number(index) is not { } ordinal)
            {
                return Refuse(
                    $"chooses its value on {Shape(chosen.IndexTerm)} against {Shape(index)}, " +
                    "which is no ordinal");
            }

            byOrdinal[(long)ordinal] = value;
        }

        ByLevelType = Keyed(chosen, byOrdinal);
        return null;
    }

    private double? Case(KismetExpression? expression, int depth) =>
        expression is EX_SwitchValue chosen
            ? Switched(chosen, depth)
            : Scalable(expression, depth) ?? Number(expression, depth);

    private IReadOnlyDictionary<string, double>? Keyed(
        EX_SwitchValue chosen, IReadOnlyDictionary<long, double> byOrdinal)
    {
        if (mappings is null || Enumerated(chosen.IndexTerm) is not { } enumerated)
        {
            Refuse($"answers a different value per {Shape(chosen.IndexTerm)}, which names no enum, so the " +
                   "values cannot be keyed by anything the export states");

            return null;
        }

        var members = EnumOrdinals.Members(mappings, enumerated);
        var named = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (ordinal, value) in byOrdinal)
        {
            if (!members.TryGetValue(ordinal, out var member))
            {
                Refuse($"answers a value for {enumerated} {ordinal}, which that enum has no member for");

                return null;
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

    private double? Scalable(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is EX_SwitchValue chosen) return Switched(chosen, depth);
        if (Struct(expression, depth) is not { } scalable) return null;

        var (multiplier, row) = scalable;
        if (row is null) return multiplier;

        if (constants.Lookup(row).Record?.Value is not { } value)
        {
            return Refuse(
                $"reads its value off row {row}, which no ability constants table gives a number for");
        }

        return multiplier * value;
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

    private double? Refuse(string what) => Unreadable is null ? null : throw Unreadable(what);
}
