using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Tooltip;

public sealed record CustomMagnitudes(CurveBinding? Duration, IReadOnlyList<string> Captures)
{
    public static readonly CustomMagnitudes None = new(null, []);
}

public static class MagnitudeCalculations
{
    private const string Function = "CalculateBaseMagnitude";
    private const string DurationField = "DurationMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string ClassField = "CalculationClassMagnitude";
    private const string DurationSlot = "DurationMagnitude.CustomMagnitude.CalculationClassMagnitude";

    public static CustomMagnitudes Of(UObject cdo, AbstractFileProvider provider, ConstantsTable constants)
    {
        CurveBinding? duration = null;
        List<string>? captures = null;

        foreach (var property in PropertyWalk.Walk(cdo))
        {
            if (!property.Name.Equals(ClassField, StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } classPath) continue;
            if (Calculation(provider, classPath) is not { } calculation) continue;

            foreach (var attribute in GameplayEffects.Captures(calculation) ?? [])
            {
                if (!(captures ??= []).Contains(attribute)) captures.Add(attribute);
            }

            if (duration is not null) continue;
            if (!property.Path.Equals(DurationSlot, StringComparison.Ordinal)) continue;
            if (PropertyWalk.GetStruct(cdo, DurationField) is not { } magnitude) continue;
            if (PropertyWalk.GetStruct(magnitude, CustomField) is not { } custom) continue;
            if (DynamicTextFloats.Body(calculation, Function) is not { Length: > 0 } bytecode) continue;
            if (new Walk(calculation, constants, bytecode).Answer() is not { } computed) continue;

            var value = Scaled(custom, "Coefficient", 1, constants)
                * (computed + Scaled(custom, "PreMultiplyAdditiveValue", 0, constants))
                + Scaled(custom, "PostMultiplyAdditiveValue", 0, constants);

            duration = new CurveBinding(
                "DurationMagnitude.ScalableFloatMagnitude",
                AbilityPresentationIndex.AssetName(classPath))
            {
                Value = Math.Round(value, 10),
                Computed = true,
                ComputedFrom = Rows(bytecode, calculation, constants),
            };
        }

        return duration is null && captures is null
            ? CustomMagnitudes.None
            : new CustomMagnitudes(duration, captures ?? []);
    }

    private static UObject? Calculation(AbstractFileProvider provider, string classPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
    }

    private static double Scaled(IPropertyHolder custom, string field, double fallback, ConstantsTable constants)
    {
        if (PropertyWalk.GetStruct(custom, field) is not { } scalable) return fallback;

        var value = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue);
        var row = PropertyWalk.GetStruct(scalable, "Curve") is { } curve
            ? PropertyWalk.GetString(curve, "RowName")
            : null;

        if (row is null || row.Length == 0 || row.Equals("None", StringComparison.Ordinal))
        {
            return value ?? fallback;
        }

        return constants.Lookup(row).Record?.Value is { } resolved ? (value ?? 1) * resolved : fallback;
    }

    public static double? Reduce(UObject calculation, ConstantsTable constants)
    {
        if (DynamicTextFloats.Body(calculation, Function) is not { Length: > 0 } bytecode) return null;

        return new Walk(calculation, constants, bytecode).Answer();
    }

    private static string[] Rows(KismetExpression[] bytecode, UObject calculation, ConstantsTable constants)
    {
        var rows = new List<string>();

        foreach (var binding in CurveBindings.Collect(calculation, constants)) rows.Add(binding.RowName);
        foreach (var statement in bytecode) InlineRows(statement, rows, 0);

        return [.. rows.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static void InlineRows(KismetExpression? expression, List<string> rows, int depth)
    {
        if (expression is null || depth > 24) return;

        if (expression is EX_StructConst { Struct.Name: "CurveTableRowHandle" } handle)
        {
            foreach (var name in handle.Properties.OfType<EX_NameConst>()) rows.Add(name.Value.Text);
            return;
        }

        foreach (var child in Children(expression)) InlineRows(child, rows, depth + 1);
    }

    private static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Variable, let.Assignment],
        EX_LetObj let => [let.Variable, let.Assignment],
        EX_LetBool let => [let.Variable, let.Assignment],
        EX_Context context => [context.ObjectExpression, context.ContextExpression],
        EX_Cast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_JumpIfNot jump => [jump.BooleanExpression],
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        EX_StructConst constant => constant.Properties,
        EX_SwitchValue chosen =>
            [chosen.IndexTerm, .. chosen.Cases.SelectMany(c => new KismetExpression?[] { c.CaseIndexValueTerm, c.CaseTerm }), chosen.DefaultTerm],
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };

    private sealed class Walk(UObject cdo, ConstantsTable constants, KismetExpression[] bytecode)
    {
        private const int Budget = 1024;
        private const int MaxDepth = 32;

        private readonly Dictionary<string, KismetExpression> _locals = new(StringComparer.Ordinal);

        public double? Answer()
        {
            var at = 0;

            for (var step = 0; step < Budget && at < bytecode.Length; step++)
            {
                switch (bytecode[at])
                {
                    case EX_Return returned:
                        return Number(returned.ReturnExpression);

                    case EX_JumpIfNot branch:
                        if (Truth(branch.BooleanExpression) is not { } taken) return null;
                        at = taken ? at + 1 : At(branch.CodeOffset);
                        if (at < 0) return null;
                        continue;

                    case EX_Jump jump:
                        at = At(jump.CodeOffset);
                        if (at < 0) return null;
                        continue;

                    case EX_EndOfScript:
                        return null;

                    default:
                        if (!Bind(bytecode[at])) return null;
                        at++;
                        continue;
                }
            }

            return null;
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

            return -1;
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
                case EX_IntConst value: return value.Value != 0;
                case EX_ByteConst value: return value.Value != 0;
                case EX_DynamicCast: return true;
                case EX_Cast cast: return Truth(cast.Target, depth + 1);
                case EX_Context context: return Truth(context.ContextExpression, depth + 1);

                case EX_CallMath call: return CalledTruth(call, depth);

                case EX_LocalVirtualFunction call: return NamedTruth(call.VirtualFunctionName.Text);
                case EX_VirtualFunction call: return NamedTruth(call.VirtualFunctionName.Text);
                case EX_LocalFinalFunction call: return NamedTruth(call.StackNode.Name);
                case EX_FinalFunction call: return NamedTruth(call.StackNode.Name);
            }

            return Bound(expression) is { } assigned ? Truth(assigned, depth + 1) : null;
        }

        private static bool? NamedTruth(string function) => function switch
        {
            "HasMatchingGameplayTag" => false,
            "HasAnyMatchingGameplayTags" => false,
            "HasAllMatchingGameplayTags" => false,
            "HasTag" => false,
            "HasAnyTags" => false,
            "HasAllTags" => false,
            _ => null,
        };

        private bool? CalledTruth(EX_CallMath call, int depth)
        {
            var function = call.StackNode.Name;

            if (function.Equals("IsValid", StringComparison.Ordinal)) return true;
            if (NamedTruth(function) is { } known) return known;

            if (function.Equals("Not_PreBool", StringComparison.Ordinal))
            {
                return Truth(call.Parameters.FirstOrDefault(), depth + 1) is { } inner ? !inner : null;
            }

            if (function.Equals("BooleanAND", StringComparison.Ordinal))
            {
                var a = Truth(call.Parameters.ElementAtOrDefault(0), depth + 1);
                var b = Truth(call.Parameters.ElementAtOrDefault(1), depth + 1);
                if (a is false || b is false) return false;
                return a is true && b is true ? true : null;
            }

            if (function.Equals("BooleanOR", StringComparison.Ordinal))
            {
                var a = Truth(call.Parameters.ElementAtOrDefault(0), depth + 1);
                var b = Truth(call.Parameters.ElementAtOrDefault(1), depth + 1);
                if (a is true || b is true) return true;
                return a is false && b is false ? false : null;
            }

            var comparison = function.Split('_')[0];
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
                case EX_SwitchValue chosen: return Switched(chosen, depth);
            }

            return Bound(expression) is { } assigned ? Number(assigned, depth + 1) : null;
        }

        private double? Called(EX_CallMath call, int depth)
        {
            var function = call.StackNode.Name;

            double? Argument(int index) => Number(call.Parameters.ElementAtOrDefault(index), depth + 1);

            if (function.StartsWith("GetScalableFloat", StringComparison.Ordinal))
            {
                return Scalable(call.Parameters.FirstOrDefault(), depth + 1);
            }

            if (function.StartsWith("Multiply_", StringComparison.Ordinal))
            {
                var a = Argument(0);
                var b = Argument(1);
                if (a == 0 || b == 0) return 0;
                return a is { } left && b is { } right ? left * right : null;
            }

            if (function.StartsWith("Add_", StringComparison.Ordinal))
            {
                return Argument(0) is { } left && Argument(1) is { } right ? left + right : null;
            }

            if (function.StartsWith("Subtract_", StringComparison.Ordinal))
            {
                return Argument(0) is { } left && Argument(1) is { } right ? left - right : null;
            }

            if (function.StartsWith("Divide_", StringComparison.Ordinal))
            {
                return Argument(0) is { } left && Argument(1) is { } right and not 0 ? left / right : null;
            }

            if (function is "SelectFloat" or "SelectInt" or "SelectDouble")
            {
                return Truth(call.Parameters.ElementAtOrDefault(2), depth + 1) switch
                {
                    true => Argument(0),
                    false => Argument(1),
                    null => null,
                };
            }

            if (function is "FClamp" or "Clamp")
            {
                return Argument(0) is { } value && Argument(1) is { } least && Argument(2) is { } most
                    ? Math.Clamp(value, least, most)
                    : null;
            }

            if (function is "FMax" or "Max")
            {
                return Argument(0) is { } left && Argument(1) is { } right ? Math.Max(left, right) : null;
            }

            if (function is "FMin" or "Min")
            {
                return Argument(0) is { } left && Argument(1) is { } right ? Math.Min(left, right) : null;
            }

            if (function.Equals("Round", StringComparison.Ordinal))
            {
                return Argument(0) is { } value ? Math.Floor(value + 0.5) : null;
            }

            if (function.Equals("FTrunc", StringComparison.Ordinal))
            {
                return Argument(0) is { } value ? Math.Truncate(value) : null;
            }

            if (function.Equals("FFloor", StringComparison.Ordinal))
            {
                return Argument(0) is { } value ? Math.Floor(value) : null;
            }

            if (function.Equals("FCeil", StringComparison.Ordinal))
            {
                return Argument(0) is { } value ? Math.Ceiling(value) : null;
            }

            if (function.Equals("Loge", StringComparison.Ordinal))
            {
                return Argument(0) is { } value and > 0 ? Math.Log(value) : null;
            }

            if (function.Equals("Log", StringComparison.Ordinal))
            {
                return Argument(0) is { } value and > 0 && Argument(1) is { } logBase and > 0 and not 1
                    ? Math.Log(value) / Math.Log(logBase)
                    : null;
            }

            if (function.StartsWith("Conv_", StringComparison.Ordinal) ||
                function.StartsWith("MakeLiteral", StringComparison.Ordinal))
            {
                return Argument(0);
            }

            return null;
        }

        private double? Switched(EX_SwitchValue chosen, int depth)
        {
            var answers = new SortedSet<double>();

            foreach (var branch in chosen.Cases)
            {
                if (Number(branch.CaseTerm, depth + 1) is not { } value) return null;
                answers.Add(value);
            }

            if (Number(chosen.DefaultTerm, depth + 1) is { } fallback) answers.Add(fallback);

            return answers.Count == 1 ? answers.Min : null;
        }

        private double? Scalable(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;
            if (expression is EX_SwitchValue chosen) return Switched(chosen, depth);
            if (Struct(expression, depth) is not { } scalable) return null;

            var (multiplier, row) = scalable;
            if (row is null) return multiplier;

            return constants.Lookup(row).Record?.Value is { } value ? multiplier * value : null;
        }

        private (double Multiplier, string? Row)? Struct(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;
            if (expression is EX_StructConst constant) return Inline(constant);
            if (Bound(expression) is { } assigned) return Struct(assigned, depth + 1);
            if (DynamicTextFloats.Name(expression) is not { } property) return null;
            if (PropertyWalk.GetStruct(cdo, property) is not { } scalable) return null;

            var row = PropertyWalk.GetStruct(scalable, "Curve") is { } curve
                ? PropertyWalk.GetString(curve, "RowName")
                : null;

            return (PropertyWalk.AsNumber(PropertyWalk.Get(scalable, "Value")?.GenericValue) ?? 1, Named(row));
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

                    case EX_StructConst handle
                        when handle.Struct.Name.Equals("CurveTableRowHandle", StringComparison.Ordinal):
                        row = handle.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text;
                        break;
                }
            }

            return (multiplier ?? 1, Named(row));
        }

        private static string? Named(string? row) =>
            row is null || row.Length == 0 || row.Equals("None", StringComparison.Ordinal) ? null : row;
    }
}
