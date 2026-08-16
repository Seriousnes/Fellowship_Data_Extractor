using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed record DynamicTextTerm(string Attribute, double Coefficient);

public sealed record DynamicTextReading
{
    public required double Coefficient { get; init; }

    public string? Attribute { get; init; }

    public bool ScalesWithExpertise { get; init; }

    public IReadOnlyList<DynamicTextTerm>? Terms { get; init; }

    public double? Addend { get; init; }

    public double? Spread { get; init; }

    public bool ScalesWithHaste { get; init; }

    public bool IsNumber => Attribute is null && Terms is null && !ScalesWithExpertise;
}

public sealed class DynamicTextFloatBody
{
    public static readonly DynamicTextFloatBody Empty =
        new(null, new Dictionary<string, KismetExpression>(), new HashSet<string>(), false);

    private readonly KismetExpression? _returned;
    private readonly IReadOnlyDictionary<string, KismetExpression> _locals;
    private readonly IReadOnlySet<string> _unity;
    private readonly bool _haste;

    internal DynamicTextFloatBody(
        KismetExpression? returned,
        IReadOnlyDictionary<string, KismetExpression> locals,
        IReadOnlySet<string> unity,
        bool haste)
    {
        _returned = returned;
        _locals = locals;
        _unity = unity;
        _haste = haste;
    }

    public DynamicTextReading? Read(
        int identifier, Func<string, double?> scalable, Func<string, string, double?>? referenced = null)
    {
        if (_returned is null) return null;

        var reader = new DynamicTextArithmetic(_locals, identifier, scalable, referenced) { Unity = _unity };

        return reader.Of(_returned) is not { } value
            ? null
            : DynamicTextArithmetic.Published(value) with { ScalesWithHaste = _haste };
    }
}

public static class DynamicTextRanges
{
    private const string Function = "ResolveDynamicTextFloatRange";
    private const string Minimum = "MinValue";
    private const string Maximum = "MaxValue";

    public static DynamicTextReading? For(
        UObject cdo,
        int identifier,
        Func<string, double?> scalable,
        Func<string, string, double?>? referenced = null)
    {
        if (DynamicTextFloats.Body(cdo, Function) is not { Length: > 0 } bytecode) return null;

        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        KismetExpression? low = null, high = null;

        foreach (var statement in bytecode)
        {
            if (DynamicTextFloats.Assigned(statement) is not var (variable, assignment)) continue;
            if (DynamicTextFloats.Name(variable) is not { } name) continue;

            if (name == Minimum) low = assignment;
            else if (name == Maximum) high = assignment;
            else locals[name] = assignment;
        }

        if (low is null || high is null) return null;

        var reader = new DynamicTextArithmetic(locals, identifier, scalable, referenced);

        if (reader.Of(low) is not { } least || reader.Of(high) is not { } most) return null;
        if (!string.Equals(least.Attribute, most.Attribute, StringComparison.Ordinal)) return null;
        if (!DynamicTextArithmetic.Same(least.Bracket, most.Bracket)) return null;

        var middle = (least.Scale + most.Scale) / 2;
        if (middle == 0) return null;

        var spread = (most.Scale - least.Scale) / middle;

        return DynamicTextArithmetic.Published(least with { Scale = middle }) with
        {
            Spread = spread > 0 ? Math.Round(spread, 10) : null,
        };
    }
}

internal sealed record DynamicTextBracket(double Constant, IReadOnlyDictionary<string, double> Parts);

internal sealed record DynamicTextValue(double Scale, string? Attribute, DynamicTextBracket? Bracket);

internal sealed class DynamicTextArithmetic(
    IReadOnlyDictionary<string, KismetExpression> locals,
    int identifier,
    Func<string, double?> scalable,
    Func<string, string, double?>? referenced = null)
{
    private const string Expertise = "Expertise";
    private const string ScalableRead = "GetScalableFloat";
    private const string AttributeRead = "GetFloatAttribute";
    private const string BaseAttributeRead = "GetFloatAttributeBase";
    private const string Select = "SelectFloat";
    private const string Convert = "Conv_";
    private const string Multiply = "Multiply_";
    private const string Divide = "Divide_";
    private const string Add = "Add_";
    private const string Subtract = "Subtract_";
    private const string StackCount = "GetActiveGameplayEffectStackCount";
    private const string DefaultObject = "GetDefaultObject";
    private const int Digits = 10;

    private static readonly string[] Rounding = ["Round", "FTrunc", "FCeil", "FFloor"];

    public IReadOnlySet<string> Unity { get; init; } = new HashSet<string>();

    public DynamicTextValue? Of(KismetExpression? expression, int depth = 0)
    {
        if (expression is null || depth > 32) return null;

        switch (expression)
        {
            case EX_DoubleConst value: return Number(value.Value);
            case EX_FloatConst value: return Number(value.Value);
            case EX_IntConst value: return Number(value.Value);

            case EX_Cast cast: return Of(cast.Target, depth + 1);

            case EX_LocalVariable or EX_LocalOutVariable
                when DynamicTextFloats.Name(expression) is { } scaler && Unity.Contains(scaler):
                return Number(1);

            case EX_LocalVariable or EX_LocalOutVariable or EX_InstanceVariable
                when DynamicTextFloats.Name(expression) is { } name && locals.TryGetValue(name, out var bound):
                return Of(bound, depth + 1);

            case EX_SwitchValue chosen: return Of(Case(chosen), depth + 1);

            case EX_CallMath call: return Called(call, depth);
        }

        return null;
    }

    public static bool Same(DynamicTextBracket? first, DynamicTextBracket? second)
    {
        if (first is null || second is null) return first is null && second is null;
        if (first.Constant != second.Constant) return false;
        if (first.Parts.Count != second.Parts.Count) return false;

        foreach (var (attribute, coefficient) in first.Parts)
        {
            if (!second.Parts.TryGetValue(attribute, out var other) || other != coefficient) return false;
        }

        return true;
    }

    public static DynamicTextReading Published(DynamicTextValue value)
    {
        var reading = new DynamicTextReading
        {
            Coefficient = Math.Round(value.Scale, Digits),
            Attribute = value.Attribute,
        };

        if (value.Bracket is not { } bracket) return reading;

        if (bracket.Constant == 1 &&
            bracket.Parts.Count == 1 &&
            bracket.Parts.TryGetValue(Expertise, out var unity) &&
            unity == 1)
        {
            return reading with { ScalesWithExpertise = true };
        }

        return reading with
        {
            Terms =
            [
                .. bracket.Parts
                    .OrderBy(part => part.Key, StringComparer.Ordinal)
                    .Select(part => new DynamicTextTerm(part.Key, Math.Round(part.Value, Digits))),
            ],
            Addend = Math.Round(bracket.Constant, Digits),
        };
    }

    private static DynamicTextValue Number(double value) => new(value, null, null);

    private KismetExpression? Case(EX_SwitchValue chosen)
    {
        foreach (var branch in chosen.Cases)
        {
            if (branch.CaseIndexValueTerm is EX_IntConst index && index.Value == identifier)
            {
                return branch.CaseTerm;
            }
        }

        return null;
    }

    private DynamicTextValue? Called(EX_CallMath call, int depth)
    {
        var function = call.StackNode.Name;

        if (function.StartsWith(ScalableRead, StringComparison.Ordinal))
        {
            var argument = call.Parameters.FirstOrDefault();

            if (Across(argument, depth) is { } borrowed) return borrowed;

            return Property(argument, depth) is { } property && scalable(property) is { } value
                ? Number(value)
                : null;
        }

        if (function.Equals(AttributeRead, StringComparison.Ordinal) ||
            function.Equals(BaseAttributeRead, StringComparison.Ordinal))
        {
            return Attribute(call) is { } attribute ? new DynamicTextValue(1, attribute, null) : null;
        }

        if (function.Equals(StackCount, StringComparison.Ordinal)) return Number(0);

        if (function.StartsWith(Convert, StringComparison.Ordinal) ||
            Array.Exists(Rounding, name => function.Equals(name, StringComparison.Ordinal)))
        {
            return Of(call.Parameters.FirstOrDefault(), depth + 1);
        }

        if (function.StartsWith(Multiply, StringComparison.Ordinal))
        {
            return Multiplied(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                              Of(call.Parameters.ElementAtOrDefault(1), depth + 1));
        }

        if (function.StartsWith(Divide, StringComparison.Ordinal))
        {
            return Divided(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                           Of(call.Parameters.ElementAtOrDefault(1), depth + 1));
        }

        if (function.StartsWith(Add, StringComparison.Ordinal))
        {
            return Combined(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                            Of(call.Parameters.ElementAtOrDefault(1), depth + 1), 1);
        }

        if (function.StartsWith(Subtract, StringComparison.Ordinal))
        {
            return Combined(Of(call.Parameters.ElementAtOrDefault(0), depth + 1),
                            Of(call.Parameters.ElementAtOrDefault(1), depth + 1), -1);
        }

        if (function.Equals(Select, StringComparison.Ordinal))
        {
            return Of(call.Parameters.ElementAtOrDefault(1), depth + 1);
        }

        return null;
    }

    private string? Property(KismetExpression? argument, int depth)
    {
        if (argument is null || depth > 32) return null;

        return argument switch
        {
            EX_Cast cast => Property(cast.Target, depth + 1),
            EX_SwitchValue chosen => Property(Case(chosen), depth + 1),
            EX_CallMath call when call.Parameters.Length == 1 => Property(call.Parameters[0], depth + 1),
            _ => DynamicTextFloats.Name(argument),
        };
    }

    private DynamicTextValue? Across(KismetExpression? argument, int depth)
    {
        if (referenced is null) return null;
        if (Unwrapped(argument, depth) is not EX_Context context) return null;
        if (DynamicTextFloats.Name(context.ContextExpression) is not { } property) return null;
        if (Holder(context.ObjectExpression, depth) is not { } holder) return null;

        return referenced(holder, property) is { } value ? Number(value) : null;
    }

    private KismetExpression? Unwrapped(KismetExpression? expression, int depth) =>
        expression is null || depth > 32
            ? null
            : expression switch
            {
                EX_Cast cast => Unwrapped(cast.Target, depth + 1),
                EX_SwitchValue chosen => Unwrapped(Case(chosen), depth + 1),
                _ => expression,
            };

    private string? Holder(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > 32) return null;

        if (expression is EX_LocalVariable or EX_LocalOutVariable or EX_InstanceVariable &&
            DynamicTextFloats.Name(expression) is { } local &&
            locals.TryGetValue(local, out var bound))
        {
            return Holder(bound, depth + 1);
        }

        return expression is EX_CallMath { StackNode.Name: DefaultObject } call
            ? DynamicTextFloats.Name(call.Parameters.FirstOrDefault())
            : null;
    }

    private static string? Attribute(EX_CallMath call)
    {
        foreach (var parameter in call.Parameters)
        {
            if (parameter is not EX_StructConst holder) continue;

            foreach (var member in holder.Properties)
            {
                if (member is EX_StringConst named) return named.Value;
            }
        }

        return null;
    }

    private static DynamicTextBracket? Linear(DynamicTextValue value)
    {
        if (value.Attribute is not null)
        {
            return value.Bracket is not null
                ? null
                : new DynamicTextBracket(
                    0,
                    new Dictionary<string, double>(StringComparer.Ordinal) { [value.Attribute] = value.Scale });
        }

        if (value.Bracket is not { } bracket)
        {
            return new DynamicTextBracket(value.Scale, new Dictionary<string, double>(StringComparer.Ordinal));
        }

        var parts = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (attribute, coefficient) in bracket.Parts) parts[attribute] = coefficient * value.Scale;

        return new DynamicTextBracket(bracket.Constant * value.Scale, parts);
    }

    private static DynamicTextValue Collapsed(DynamicTextBracket bracket)
    {
        if (bracket.Parts.Count == 0) return Number(bracket.Constant);

        if (bracket.Constant == 0 && bracket.Parts.Count == 1)
        {
            var (attribute, coefficient) = bracket.Parts.First();
            return new DynamicTextValue(coefficient, attribute, null);
        }

        return new DynamicTextValue(1, null, bracket);
    }

    private static DynamicTextValue? Combined(DynamicTextValue? first, DynamicTextValue? second, double sign)
    {
        if (first is not { } a || second is not { } b) return null;
        if (Linear(a) is not { } left || Linear(b) is not { } right) return null;

        var parts = new Dictionary<string, double>(left.Parts, StringComparer.Ordinal);

        foreach (var (attribute, coefficient) in right.Parts)
        {
            var added = parts.GetValueOrDefault(attribute) + (sign * coefficient);

            if (added == 0) parts.Remove(attribute);
            else parts[attribute] = added;
        }

        return Collapsed(new DynamicTextBracket(left.Constant + (sign * right.Constant), parts));
    }

    private static DynamicTextValue? Multiplied(DynamicTextValue? first, DynamicTextValue? second)
    {
        if (first is not { } a || second is not { } b) return null;

        if (a is { Attribute: null, Bracket: null }) return Scaled(b, a.Scale);
        if (b is { Attribute: null, Bracket: null }) return Scaled(a, b.Scale);

        if (a.Attribute is not null && b.Attribute is not null) return null;
        if (a.Bracket is not null && b.Bracket is not null) return null;

        return new DynamicTextValue(a.Scale * b.Scale, a.Attribute ?? b.Attribute, a.Bracket ?? b.Bracket);
    }

    private static DynamicTextValue? Divided(DynamicTextValue? first, DynamicTextValue? second) =>
        first is { } a && second is { Attribute: null, Bracket: null, Scale: not 0 } b
            ? Scaled(a, 1 / b.Scale)
            : null;

    private static DynamicTextValue Scaled(DynamicTextValue value, double factor) =>
        value with { Scale = value.Scale * factor };
}

public static class DynamicTextFloats
{
    private const string Function = "ResolveDynamicTextFloat";
    private const string HasteScaler = "GetDefaultTimingScalerBasedOnHaste";
    private const string ScalerOutput = $"CallFunc_{HasteScaler}";
    private const string Returned = "ReturnValue";

    public static DynamicTextFloatBody Of(UObject cdo)
    {
        if (Body(cdo) is not { Length: > 0 } bytecode) return DynamicTextFloatBody.Empty;

        var locals = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        var unity = new HashSet<string>(StringComparer.Ordinal);
        var haste = false;

        foreach (var statement in bytecode)
        {
            if (Named(statement) == HasteScaler)
            {
                haste = true;

                foreach (var parameter in Parameters(statement))
                {
                    if (Name(parameter) is { } output &&
                        output.StartsWith(ScalerOutput, StringComparison.Ordinal))
                    {
                        unity.Add(output);
                    }
                }
            }

            if (Assigned(statement) is not var (variable, assignment)) continue;
            if (Name(variable) is not { } local) continue;

            locals[local] = assignment;
        }

        return locals.TryGetValue(Returned, out var returned)
            ? new DynamicTextFloatBody(returned, locals, unity, haste)
            : DynamicTextFloatBody.Empty;
    }

    internal static (KismetExpression Variable, KismetExpression Assignment)? Assigned(
        KismetExpression statement) => statement switch
    {
        EX_LetObj held => (held.Variable, held.Assignment),
        EX_Let let => (let.Variable, let.Assignment),
        _ => null,
    };

    private static KismetExpression[] Parameters(KismetExpression expression) => expression switch
    {
        EX_Context context => Parameters(context.ContextExpression),
        EX_LocalVirtualFunction call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_CallMath call => call.Parameters,
        _ => [],
    };

    private static KismetExpression[]? Body(UObject cdo) => Body(cdo, Function);

    internal static KismetExpression[]? Body(UObject cdo, string function)
    {
        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is UStruct body && export.Name.Equals(function, StringComparison.Ordinal))
            {
                return body.ScriptBytecode;
            }
        }

        return null;
    }

    private static string? Named(KismetExpression expression) => expression switch
    {
        EX_Context context => Named(context.ContextExpression),
        EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
        EX_LocalFinalFunction call => call.StackNode.Name,
        EX_CallMath call => call.StackNode.Name,
        _ => null,
    };

    internal static string? Name(KismetExpression? expression) => expression switch
    {
        EX_StructMemberContext member => Leaf(member.Property),
        EX_InstanceVariable variable => Leaf(variable.Variable),
        EX_LocalVariable variable => Leaf(variable.Variable),
        EX_LocalOutVariable variable => Leaf(variable.Variable),
        EX_Cast cast => Name(cast.Target),
        _ => null,
    };

    private static string? Leaf(FKismetPropertyPointer pointer) =>
        pointer.New?.Path is { Length: > 0 } path ? path[^1].Text : null;
}
