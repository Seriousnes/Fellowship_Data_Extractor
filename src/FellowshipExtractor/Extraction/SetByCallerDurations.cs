using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class SetByCallerDurations
{
    private const string DurationField = "DurationMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string SetByCallerField = "SetByCallerMagnitude";
    private const string DataTagField = "DataTag";
    private const string TagNameField = "TagName";
    private const string TagStruct = "GameplayTag";
    private const string KindEnum = "EGameplayEffectMagnitudeCalculation";
    private const string SetByCaller = "SetByCaller";
    private const string Assignment = "AssignTagSetByCallerMagnitude";
    private const int MagnitudeParameter = 2;
    private const int MaxDepth = 24;

    public static string? DeclaredTag(UObject? effect, TypeMappings mappings)
    {
        if (effect is null) return null;
        if (PropertyWalk.GetStruct(effect, DurationField) is not { } duration) return null;

        var kind = PropertyWalk.GetString(duration, CalculationTypeField);
        if (EnumOrdinals.Member(mappings, KindEnum, kind) != SetByCaller) return null;
        if (PropertyWalk.GetStruct(duration, SetByCallerField) is not { } supplied) return null;
        if (PropertyWalk.GetStruct(supplied, DataTagField) is not { } tag) return null;

        return PropertyWalk.GetString(tag, TagNameField);
    }

    public static (double? Duration, string? Derivation) From(
        UObject? applier, string tag, ConstantsTable constants)
    {
        if (applier is null) return (null, null);

        var values = new List<double>();
        var derivations = new List<string>();
        var assignments = 0;

        foreach (var export in applier.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var walk = new BareStateWalk(body, applier, constants, null);

            foreach (var statement in bytecode)
            {
                var calls = new List<KismetExpression[]>();
                Assignments(statement, tag, applier, calls, 0);

                foreach (var parameters in calls)
                {
                    assignments++;

                    var magnitude = parameters.ElementAtOrDefault(MagnitudeParameter);
                    if (walk.Number(magnitude) is not { } read) continue;

                    values.Add(read);
                    derivations.Add($"{read} = {walk.Text(magnitude)}");
                }

                walk.Bind(statement);
            }
        }

        if (assignments == 0 || values.Count != assignments) return (null, null);

        var distinct = values.Distinct().ToArray();

        return distinct.Length == 1
            ? (distinct[0], string.Join(" and ", derivations.Distinct(StringComparer.Ordinal)))
            : (null, null);
    }

    private static void Assignments(
        KismetExpression? expression,
        string tag,
        UObject applier,
        ICollection<KismetExpression[]> calls,
        int depth)
    {
        if (expression is null || depth > MaxDepth) return;

        if (expression is EX_CallMath call && call.StackNode.Name.Equals(Assignment, StringComparison.Ordinal))
        {
            if (call.Parameters.Any(parameter => Tag(parameter, applier) == tag)) calls.Add(call.Parameters);
        }

        foreach (var child in Children(expression)) Assignments(child, tag, applier, calls, depth + 1);
    }

    private static string? Tag(KismetExpression? expression, UObject applier)
    {
        if (expression is EX_StructConst { Struct.Name: TagStruct } inline)
        {
            return inline.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text;
        }

        if (DynamicTextFloats.Name(expression) is not { } property) return null;
        if (PropertyWalk.GetStruct(applier, property) is not { } holder) return null;

        return PropertyWalk.GetString(holder, TagNameField);
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
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };
}
