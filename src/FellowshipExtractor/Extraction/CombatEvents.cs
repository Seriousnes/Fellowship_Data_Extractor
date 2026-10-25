using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class CombatEvents
{
    private const string WaitTask = "WaitForCombatEvents";
    private const int MaxDepth = 8;

    public static IReadOnlyDictionary<int, IReadOnlyList<string>> Of(UStruct body, UObject cdo, BytecodeGates graph)
    {
        var heard = new Dictionary<int, IReadOnlyList<string>>();

        foreach (var (call, reached, _) in Bound(body, cdo, graph, called => called == WaitTask))
        {
            string[] tags = [.. RowSinks.Parameters(call).SelectMany(parameter => Tags(parameter, 0)).Distinct(StringComparer.Ordinal)];
            if (tags.Length == 0) continue;

            foreach (var at in reached)
            {
                heard[at] = heard.TryGetValue(at, out var held) ? [.. held.Union(tags, StringComparer.Ordinal)] : tags;
            }
        }

        return heard;
    }

    public static IEnumerable<(KismetExpression Call, IReadOnlySet<int> Reached, string? Event)> Bound(
        UStruct body, UObject cdo, BytecodeGates graph, Func<string, bool> wanted)
    {
        var code = body.ScriptBytecode ?? [];

        var tasks = new Dictionary<string, KismetExpression>(StringComparer.Ordinal);
        var delegates = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var statement in code)
        {
            if (statement is EX_LetObj { Variable: { } variable, Assignment: { } assigned } &&
                DynamicTextFloats.Name(variable) is { } task &&
                RowSinks.Invoking(assigned) is { } call && RowSinks.Calling(call) is { } called && wanted(called))
            {
                tasks[task] = call;
            }

            if (statement is EX_BindDelegate { Delegate: { } bound } binding && DynamicTextFloats.Name(bound) is { } local)
            {
                delegates[local] = binding.FunctionName.Text;
            }
        }

        if (tasks.Count == 0) yield break;

        var functions = (cdo.Owner?.GetExports() ?? [])
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .GroupBy(export => export.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var statement in code)
        {
            if (statement is not EX_AddMulticastDelegate { Delegate: EX_Context { ObjectExpression: { } owner, ContextExpression: var member }, DelegateToAdd: { } added }) continue;
            if (DynamicTextFloats.Name(owner) is not { } task || !tasks.TryGetValue(task, out var call)) continue;
            if (DynamicTextFloats.Name(added) is not { } local || !delegates.TryGetValue(local, out var function)) continue;
            if (!functions.TryGetValue(function, out var handler)) continue;

            foreach (var entered in handler.ScriptBytecode!)
            {
                foreach (var entry in BytecodeGates.Entered(entered, body.Name, 0)) yield return (call, graph.From(entry), DynamicTextFloats.Name(member));
            }
        }
    }

    private static IEnumerable<string> Tags(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (expression is EX_NameConst name && !name.Value.IsNone && name.Value.Text.Contains('.'))
        {
            yield return name.Value.Text;
            yield break;
        }

        foreach (var child in RowSinks.Children(expression))
        {
            foreach (var tag in Tags(child, depth + 1)) yield return tag;
        }
    }
}
