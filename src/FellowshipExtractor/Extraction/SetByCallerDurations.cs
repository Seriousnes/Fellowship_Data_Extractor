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
    private const int SpecDepth = 8;

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

    public static (double? Duration, string? Derivation, IReadOnlyList<string> Gates) From(
        UObject? applier, string tag, ConstantsTable constants, string? effectClass = null)
    {
        if (applier is null) return (null, null, []);

        var values = new List<double>();
        var derivations = new List<string>();
        var assignments = 0;
        var gates = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var export in applier.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var walk = new BareStateWalk(body, applier, constants, null);
            var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in bytecode)
            {
                if (statement is EX_Let { Variable: { } variable, Assignment: { } assigned } &&
                    DynamicTextFloats.Name(variable) is { } local)
                {
                    if (!locals.TryGetValue(local, out var held)) locals[local] = held = [];
                    held.Add(assigned);
                }
            }

            foreach (var statement in bytecode)
            {
                var calls = new List<KismetExpression[]>();
                Assignments(statement, tag, applier, calls, 0);

                foreach (var parameters in calls)
                {
                    if (effectClass is not null &&
                        Spec(parameters.FirstOrDefault(), locals, applier, 0, []) is { } spec &&
                        !spec.Equals(effectClass, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    assignments++;

                    var magnitude = parameters.ElementAtOrDefault(MagnitudeParameter);
                    var tested = walk.Gates.ToHashSet(StringComparer.Ordinal);
                    var read = walk.Number(magnitude);

                    gates.UnionWith(walk.Gates.Except(tested, StringComparer.Ordinal));
                    if (read is null) continue;

                    values.Add(read.Value);
                    derivations.Add($"{read} = {walk.Text(magnitude)}");
                }

                walk.Bind(statement);
            }
        }

        if (assignments == 0 || values.Count != assignments) return (null, null, []);

        var distinct = values.Distinct().ToArray();

        return distinct.Length == 1
            ? (distinct[0], string.Join(" and ", derivations.Distinct(StringComparer.Ordinal)), [.. gates])
            : (null, null, []);
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

    private static string? Spec(
        KismetExpression? expression,
        IReadOnlyDictionary<string, List<KismetExpression>> locals,
        UObject applier,
        int depth,
        HashSet<string> visiting)
    {
        if (expression is null || depth > SpecDepth) return null;

        switch (expression)
        {
            case EX_ObjectConst constant when PropertyWalk.AsString(constant.Value) is { } path:
                return Leaf(path);
            case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property &&
                                                  PropertyWalk.Get(applier, property)?.GenericValue is FPackageIndex index &&
                                                  PropertyWalk.AsString(index) is { } path:
                return Leaf(path);
            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                if (!visiting.Add(local) || !locals.TryGetValue(local, out var values)) return null;
                return values.Select(value => Spec(value, locals, applier, depth + 1, visiting))
                    .FirstOrDefault(found => found is not null);
        }

        return Children(expression).Select(child => Spec(child, locals, applier, depth + 1, visiting))
            .FirstOrDefault(found => found is not null);
    }

    private static string Leaf(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot >= 0) name = name[(dot + 1)..];

        return name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;
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
