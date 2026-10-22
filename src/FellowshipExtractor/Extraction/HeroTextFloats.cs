using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class HeroTextFloats
{
    private const string Function = "ResolveDynamicTextFloat";
    private const string IdentifierField = "AbilityDynamicTextFloatIdentifier";
    private const string NotEqual = "NotEqual_IntInt";
    private const string Equal = "EqualEqual_IntInt";
    private const string ScalableRead = "GetScalableFloat";
    private const string Multiply = "Multiply_";
    private const string Divide = "Divide_";
    private const string Add = "Add_";
    private const string Subtract = "Subtract_";
    private const string ValueField = "Value";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const int MaxSteps = 512;
    private const int MaxDepth = 16;

    public static double? Read(UObject ability, int identifier, string heroTag, ConstantsTable constants)
    {
        if (BareStateWalk.Compiled(ability, Function)?.ScriptBytecode is not { Length: > 0 } bytecode) return null;
        if (Returned(bytecode, identifier) is not var (returned, assigned)) return null;

        return Evaluated(returned, assigned, ability, heroTag, constants, 0);
    }

    private static (KismetExpression Returned, Dictionary<string, KismetExpression> Assigned)? Returned(
        KismetExpression[] bytecode, int identifier)
    {
        var at = new Dictionary<int, int>();
        for (var index = 0; index < bytecode.Length; index++) at[bytecode[index].StatementIndex] = index;

        var assigned = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        var booleans = new Dictionary<string, bool>(StringComparer.Ordinal);

        for (int pc = 0, steps = 0; pc >= 0 && pc < bytecode.Length && steps < MaxSteps; steps++)
        {
            switch (bytecode[pc])
            {
                case EX_LetBool { Variable: var variable } let when Name(variable) is { } name:
                    if (Compared(let.Assignment, identifier) is { } outcome) booleans[name] = outcome;
                    pc++;
                    break;
                case EX_Let { Variable: var variable } let when Name(variable) is { } name:
                    assigned[name] = let.Assignment;
                    pc++;
                    break;
                case EX_JumpIfNot jump:
                    pc = Name(jump.BooleanExpression) is { } condition &&
                         booleans.TryGetValue(condition, out var holds) && !holds
                        ? at.GetValueOrDefault((int)jump.CodeOffset, -1)
                        : pc + 1;
                    break;
                case EX_Jump jump:
                    pc = at.GetValueOrDefault((int)jump.CodeOffset, -1);
                    break;
                case EX_Return returned:
                    return (returned.ReturnExpression, assigned);
                default:
                    pc++;
                    break;
            }
        }

        return null;
    }

    private static bool? Compared(KismetExpression expression, int identifier)
    {
        if (expression is not EX_CallMath call) return null;

        var negated = call.StackNode.Name switch
        {
            NotEqual => true,
            Equal => false,
            _ => (bool?)null,
        };

        if (negated is null) return null;
        if (call.Parameters.ElementAtOrDefault(0) is not EX_StructMemberContext member) return null;
        if (Leaf(member.Property) != IdentifierField) return null;
        if (call.Parameters.ElementAtOrDefault(1) is not EX_IntConst compared) return null;

        return (compared.Value == identifier) != negated.Value;
    }

    private static double? Evaluated(
        KismetExpression expression,
        IReadOnlyDictionary<string, KismetExpression> assigned,
        UObject ability,
        string heroTag,
        ConstantsTable constants,
        int depth)
    {
        if (depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_LocalVariable or EX_LocalOutVariable when Name(expression) is { } local &&
                                                               assigned.TryGetValue(local, out var bound):
                return Evaluated(bound, assigned, ability, heroTag, constants, depth + 1);
            case EX_FloatConst constant:
                return constant.Value;
            case EX_DoubleConst constant:
                return constant.Value;
            case EX_CallMath call when call.StackNode.Name.StartsWith(ScalableRead, StringComparison.Ordinal):
                return call.Parameters.FirstOrDefault() is EX_StructMemberContext member && Leaf(member.Property) is { } field
                    ? Scalable(ability, heroTag, field, constants)
                    : null;
            case EX_IntConst constant:
                return constant.Value;
            case EX_Cast cast:
                return Evaluated(cast.Target, assigned, ability, heroTag, constants, depth + 1);
            case EX_CallMath call when Operation(call.StackNode.Name) is { } operation && call.Parameters.Length == 2:
                return Evaluated(call.Parameters[0], assigned, ability, heroTag, constants, depth + 1) is { } first &&
                       Evaluated(call.Parameters[1], assigned, ability, heroTag, constants, depth + 1) is { } second
                    ? operation(first, second)
                    : null;
            default:
                return null;
        }
    }

    private static Func<double, double, double>? Operation(string function) =>
        function.StartsWith(Multiply, StringComparison.Ordinal) ? (first, second) => first * second
        : function.StartsWith(Divide, StringComparison.Ordinal) ? (first, second) => first / second
        : function.StartsWith(Add, StringComparison.Ordinal) ? (first, second) => first + second
        : function.StartsWith(Subtract, StringComparison.Ordinal) ? (first, second) => first - second
        : null;

    private static double? Scalable(UObject ability, string heroTag, string field, ConstantsTable constants)
    {
        foreach (var property in ability.Properties)
        {
            if (property.Tag?.GenericValue is not UScriptMap map) continue;

            foreach (var (key, value) in map.Properties)
            {
                if (!string.Equals(PropertyWalk.AsString(key?.GenericValue), heroTag, StringComparison.OrdinalIgnoreCase)) continue;
                if (value?.GenericValue is not FScriptStruct { StructType: IPropertyHolder entry }) continue;
                if (PropertyWalk.GetStruct(entry, field) is not { } scalable) continue;

                var multiplier = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;
                var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve
                    ? PropertyWalk.GetString(curve, RowField)
                    : null;

                if (row is null) return multiplier;

                return constants.Lookup(row).Record?.Value is { } stated ? multiplier * stated : null;
            }
        }

        return null;
    }

    private static string? Name(KismetExpression? expression) => DynamicTextFloats.Name(expression);

    private static string? Leaf(FKismetPropertyPointer pointer) =>
        pointer.New?.Path is { Length: > 0 } path ? path[^1].Text : null;
}
