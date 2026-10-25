using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public static class AbilityLinks
{
    private const string TriggersField = "AbilityTriggers";
    private const string TriggerTagField = "TriggerTag";
    private const string BypassField = "BypassCostProcs";
    private const string BypassTagField = "ProcEffectGrantedTag";
    private const string GrantedAbilitiesField = "GrantedAbilities";
    private const string GrantAbilityConfigsField = "GrantAbilityConfigs";
    private const string SendEvent = "SendGameplayEvent";
    private const string CastingHook = "ModifyCastingDuration";
    private const string Select = "SelectFloat";
    private const string AddLoose = "AddLooseGameplayTag";
    private const string StartCasting = "StartCasting";
    private const string ProcHandler = "ProcHandler";
    private const string ProcActivation = "ActivationHasProc";
    private const string AddAssetTag = "AddAssetTag";
    private const string ReturnLocal = "ReturnValue";
    private const int MaxDepth = 8;

    public static IReadOnlyList<string> Triggers(UObject? cdo) => Tagged(cdo, TriggersField, TriggerTagField);

    public static IReadOnlyList<string> Bypassed(UObject? cdo) => Tagged(cdo, BypassField, BypassTagField);

    public static IReadOnlyList<string> GrantedAbilities(UObject? effect)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var export in effect?.Owner?.GetExports() ?? [])
        {
            foreach (var property in PropertyWalk.Walk(export))
            {
                if (!property.Path.Contains(GrantedAbilitiesField, StringComparison.Ordinal) &&
                    !property.Path.Contains(GrantAbilityConfigsField, StringComparison.Ordinal)) continue;
                if (PropertyWalk.AsString(property.Value) is not { } path || !path.Contains('/')) continue;

                var leaf = path[(path.LastIndexOf('/') + 1)..];
                if (leaf.IndexOf('.') is var dot and >= 0) leaf = leaf[..dot];

                names.Add(leaf.EndsWith("_C", StringComparison.Ordinal) ? leaf[..^2] : leaf);
            }
        }

        return [.. names];
    }

    public static IReadOnlyList<string> Sent(UObject? cdo) =>
        [.. Calls(cdo, call => call.Contains(SendEvent, StringComparison.Ordinal))
            .SelectMany(found => found.Parameters.Where(parameter => parameter is not EX_InstanceVariable).SelectMany(parameter => Tags(parameter, cdo!, 0)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    public static IReadOnlyList<string> Loose(UObject? cdo)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var export in cdo?.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            var locals = Locals(bytecode);

            foreach (var statement in bytecode)
            {
                foreach (var (name, parameters) in Invocations(statement, 0))
                {
                    if (!name.StartsWith(AddLoose, StringComparison.Ordinal)) continue;

                    foreach (var parameter in parameters) tags.UnionWith(Tags(parameter, cdo!, 0, locals));
                }
            }
        }

        return [.. tags];
    }

    private static Dictionary<string, List<KismetExpression>> Locals(KismetExpression[] bytecode)
    {
        var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

        foreach (var statement in bytecode)
        {
            var (variable, assigned) = statement switch
            {
                EX_Let let => (let.Variable, let.Assignment),
                EX_LetBool let => (let.Variable, let.Assignment),
                _ => (null, null),
            };

            if (variable is null || assigned is null || DynamicTextFloats.Name(variable) is not { } local) continue;

            if (!locals.TryGetValue(local, out var held)) locals[local] = held = [];
            held.Add(assigned);
        }

        return locals;
    }

    public static IReadOnlyList<string> Instant(UObject? cdo)
    {
        var tags = new SortedSet<string>(StringComparer.Ordinal);
        if (cdo is null) return [];

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            if (body.Name.Equals(CastingHook, StringComparison.Ordinal))
            {
                var locals = Locals(bytecode);

                foreach (var returned in Returned(locals, ReturnLocal, 0))
                {
                    if (RowSinks.Invoking(returned) is not { } call || RowSinks.Calling(call) is not { } name) continue;
                    if (!name.StartsWith(Select, StringComparison.Ordinal)) continue;

                    var parameters = RowSinks.Parameters(call);
                    if (parameters.Count < 3 || !Zero(parameters[0])) continue;

                    tags.UnionWith(Tags(parameters[2], cdo, 0, locals));
                }

                continue;
            }

            if (BytecodeGates.Of(body, cdo) is not { } graph) continue;

            var starts = bytecode
                .Where(statement => Invocations(statement, 0).Any(found => found.Name.EndsWith(StartCasting, StringComparison.Ordinal)))
                .Select(statement => statement.StatementIndex)
                .ToHashSet();

            if (starts.Count == 0) continue;

            var bound = CombatEvents.Bound(body, cdo, graph, _ => true).ToArray();

            var acting = bytecode
                .Where(statement => Invocations(statement, 0).Any())
                .Select(statement => statement.StatementIndex)
                .ToHashSet();

            var finishing = bound
                .Where(entry => RowSinks.Calling(entry.Call)?.EndsWith(StartCasting, StringComparison.Ordinal) == true)
                .SelectMany(entry => entry.Reached)
                .Where(acting.Contains)
                .ToHashSet();

            foreach (var handler in bound
                         .Where(entry => RowSinks.Calling(entry.Call)?.Contains(ProcHandler, StringComparison.Ordinal) == true)
                         .GroupBy(entry => entry.Call, ReferenceEqualityComparer.Instance))
            {
                var casts = handler.Any(entry => entry.Reached.Overlaps(starts));
                var skips = handler.Any(entry => !entry.Reached.Overlaps(starts) && entry.Reached.Overlaps(finishing));
                if (!casts || !skips) continue;

                tags.UnionWith(Tags(RowSinks.Parameters(handler.First().Call).ElementAtOrDefault(2), cdo, 0));
            }
        }

        return [.. tags];
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ProcEffects(UObject? cdo) =>
        Gathered(ProcStatements(cdo).Select(entry =>
        {
            var value = entry.Statement switch
            {
                EX_Let { Variable: EX_InstanceVariable, Assignment: { } assigned } => assigned,
                EX_LetObj { Variable: EX_InstanceVariable, Assignment: { } assigned } => assigned,
                _ => null,
            };

            if (value is not EX_InstanceVariable source || DynamicTextFloats.Name(source) is not { } property) return (null, entry.Tags);
            if (PropertyWalk.AsString(PropertyWalk.Get(cdo!, property)?.GenericValue) is not { } path || !path.Contains('/')) return (null, entry.Tags);

            var leaf = path[(path.LastIndexOf('/') + 1)..];
            if (leaf.IndexOf('.') is var dot and >= 0) leaf = leaf[..dot];

            return ((string?)leaf, entry.Tags);
        }));

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ProcAssetTags(UObject? cdo) =>
        Gathered(ProcStatements(cdo).SelectMany(entry => Invocations(entry.Statement, 0)
            .Where(found => found.Name.Contains(AddAssetTag, StringComparison.Ordinal))
            .SelectMany(found => found.Parameters.Where(parameter => parameter is not EX_InstanceVariable).SelectMany(parameter => Tags(parameter, cdo!, 0)))
            .Select(tag => ((string?)tag, entry.Tags))));

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Gathered(IEnumerable<(string? Key, string[] Tags)> entries)
    {
        var gated = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, tags) in entries)
        {
            if (key is null) continue;
            if (!gated.TryGetValue(key, out var held)) gated[key] = held = new SortedSet<string>(StringComparer.Ordinal);
            held.UnionWith(tags);
        }

        return gated.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)[.. entry.Value], StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<(KismetExpression Statement, string[] Tags)> ProcStatements(UObject? cdo)
    {
        if (cdo is null) yield break;

        foreach (var export in cdo.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;
            if (BytecodeGates.Of(body, cdo) is not { } graph) continue;

            var bound = CombatEvents.Bound(body, cdo, graph, called => called.Contains(ProcHandler, StringComparison.Ordinal)).ToArray();

            foreach (var handler in bound.GroupBy(entry => entry.Call, ReferenceEqualityComparer.Instance))
            {
                var entries = handler.ToArray();
                string[] tags = [.. Tags(RowSinks.Parameters(entries[0].Call).ElementAtOrDefault(2), cdo, 0)];
                if (tags.Length == 0) continue;

                foreach (var entry in entries.Where(entry => entry.Event == ProcActivation))
                {
                    var others = entries.Where(other => !ReferenceEquals(other.Reached, entry.Reached)).SelectMany(other => other.Reached).ToHashSet();

                    foreach (var statement in bytecode)
                    {
                        if (entry.Reached.Contains(statement.StatementIndex) && !others.Contains(statement.StatementIndex)) yield return (statement, tags);
                    }
                }
            }
        }
    }

    private static IEnumerable<KismetExpression> Returned(
        IReadOnlyDictionary<string, List<KismetExpression>> locals, string name, int depth)
    {
        if (depth > MaxDepth || !locals.TryGetValue(name, out var values)) yield break;

        foreach (var value in values)
        {
            var inner = value is EX_Cast cast ? cast.Target : value;

            if (inner is EX_LocalVariable or EX_LocalOutVariable && DynamicTextFloats.Name(inner) is { } next)
            {
                foreach (var found in Returned(locals, next, depth + 1)) yield return found;
            }
            else
            {
                yield return inner;
            }
        }
    }

    private static bool Zero(KismetExpression? expression) => expression switch
    {
        EX_DoubleConst constant => constant.Value == 0,
        EX_FloatConst constant => constant.Value == 0,
        EX_IntZero => true,
        EX_Cast cast => Zero(cast.Target),
        _ => false,
    };

    private static IReadOnlyList<string> Tagged(UObject? cdo, string container, string field)
    {
        if (cdo is null) return [];

        return
        [
            .. PropertyWalk.Walk(cdo)
                .Where(property => property.Path.StartsWith(container, StringComparison.Ordinal) &&
                                   property.Name.Equals(field, StringComparison.Ordinal))
                .Select(property => PropertyWalk.AsString(property.Value))
                .OfType<string>()
                .Where(tag => tag.Contains('.'))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static IEnumerable<(string Name, IReadOnlyList<KismetExpression> Parameters)> Calls(
        UObject? cdo, Func<string, bool> wanted)
    {
        foreach (var export in cdo?.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var found in Invocations(statement, 0).Where(found => wanted(found.Name))) yield return found;
            }
        }
    }

    private static IEnumerable<(string Name, IReadOnlyList<KismetExpression> Parameters)> Invocations(
        KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth * 4) yield break;

        if (RowSinks.Invoking(expression) is { } call && RowSinks.Calling(call) is { } called)
        {
            var parameters = RowSinks.Parameters(call);

            yield return (called, parameters);

            foreach (var parameter in parameters)
            {
                foreach (var nested in Invocations(parameter, depth + 1)) yield return nested;
            }

            yield break;
        }

        foreach (var child in RowSinks.Children(expression))
        {
            foreach (var nested in Invocations(child, depth + 1)) yield return nested;
        }
    }

    private static IEnumerable<string> Tags(
        KismetExpression? expression, UObject cdo, int depth,
        IReadOnlyDictionary<string, List<KismetExpression>>? locals = null)
    {
        if (expression is null || depth > MaxDepth) yield break;

        switch (expression)
        {
            case EX_LocalVariable or EX_LocalOutVariable when locals is not null &&
                                                             DynamicTextFloats.Name(expression) is { } local &&
                                                             locals.TryGetValue(local, out var values):
                foreach (var value in values)
                {
                    foreach (var tag in Tags(value, cdo, depth + 1, locals)) yield return tag;
                }

                yield break;
            case EX_NameConst name when !name.Value.IsNone && name.Value.Text.Contains('.'):
                yield return name.Value.Text;
                yield break;
            case EX_StringConst text when text.Value.Contains('.') && !text.Value.Contains('/'):
                yield return text.Value;
                yield break;
            case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property &&
                                                  PropertyWalk.Get(cdo, property)?.GenericValue is { } held:
                foreach (var tag in PropertyWalk.AsTags(held) ?? (PropertyWalk.AsString(held) is { } one &&
                                                                  one.Contains('.') && !one.Contains('/')
                             ? [one]
                             : []))
                {
                    yield return tag;
                }

                yield break;
        }

        var children = RowSinks.Invoking(expression) is { } call ? RowSinks.Parameters(call) : RowSinks.Children(expression);

        foreach (var child in children)
        {
            foreach (var tag in Tags(child, cdo, depth + 1, locals)) yield return tag;
        }
    }

}
