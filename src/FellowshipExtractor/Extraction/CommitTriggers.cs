using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Reads the waits an ability's Blueprint holds on abilities being committed: <c>WaitForAbilityCommit</c>,
/// <c>WaitForAbilityCommit_Query</c>, and <c>WaitGameplayEvent</c> or <c>WaitGameplayEvents</c> on <c>Event.CommittedAbility</c>. Each wait binds a
/// delegate, the delegate's function enters the ubergraph at an offset, and the walk from that offset reaches each
/// applied effect past the tag tests gating it.
/// </summary>
public sealed class CommitTriggers(AbstractFileProvider provider, IReadOnlySet<string> effects, Func<string, IconSource?> effectSource)
{
    private const string WaitForCommit = "WaitForAbilityCommit";
    private const string WaitForQuery = "WaitForAbilityCommit_Query";
    private const string WaitForEvents = "WaitGameplayEvents";
    private const string WaitForEvent = "WaitGameplayEvent";
    private const string CommittedEvent = "Event.CommittedAbility";
    private const string CommitDelegate = "OnCommit";
    private const string EventDelegate = "EventReceived";
    private const string Ubergraph = "ExecuteUbergraph";
    private const string TagTest = "HasMatchingGameplayTag";
    private const string Negation = "Not_PreBool";
    private const string ApplyingCall = "ApplyGameplayEffect";
    private const string SpecCall = "EffectSpec";
    private const string MakingCall = "MakeEffectSpec";
    private const string DictionaryField = "TagDictionary";
    private const string TokenStreamField = "QueryTokenStream";
    private const int RootOffset = 2;
    private const int AnyTags = 1;
    private const int AllTags = 2;
    private const int NoTags = 3;
    private const int AllExpressions = 5;
    private const string NoTag = "None";
    private const int TagParameter = 1;
    private const int MaxDepth = 24;

    public int Waits { get; private set; }

    public List<string> Unread { get; } = [];

    /// <summary>A wait's tags, and its offset in the ubergraph once the delegate resolves.</summary>
    private sealed record Wait(IReadOnlyList<string> Tags, bool AllTags, IReadOnlyList<string> Excluded, int Entry);

    /// <summary>An effect a walk reaches past its tag tests. A null effect is a spec applied with its maker out of reach.</summary>
    private sealed record Reach(string? Effect, HashSet<string> Stopping, HashSet<string> Required);

    /// <summary>The bodies of one Blueprint, with the paths and reaches read from them.</summary>
    private sealed class Walk(IReadOnlyList<UStruct> bodies, UObject cdo)
    {
        private readonly Dictionary<string, BranchPaths> _paths = new(StringComparer.Ordinal);

        public IReadOnlyList<UStruct> Bodies => bodies;

        public UObject Cdo => cdo;

        public Dictionary<(string Body, int Entry), List<Reach>> Reached { get; } = [];

        public BranchPaths PathsOf(UStruct body) =>
            _paths.TryGetValue(body.Name, out var known) ? known : _paths[body.Name] = new BranchPaths(body);
    }

    public IReadOnlyList<CommitTriggerSource> Of(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return [];

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var walk = new Walk(bodies, cdo);
        var found = new List<CommitTriggerSource>();

        foreach (var body in bodies)
        {
            foreach (var wait in Waited(body, bodies, cdo))
            {
                Waits++;
                found.AddRange(Triggers(wait, UbergraphOf(bodies) ?? body, walk));
            }
        }

        return found;
    }

    private static UStruct? UbergraphOf(IReadOnlyList<UStruct> bodies) =>
        bodies.FirstOrDefault(body => body.Name.StartsWith(Ubergraph, StringComparison.Ordinal));

    private IEnumerable<CommitTriggerSource> Triggers(Wait wait, UStruct body, Walk walk)
    {
        var byEffect = new Dictionary<string, (HashSet<string> Stopping, HashSet<string> Required)>(StringComparer.OrdinalIgnoreCase);
        var reached = Reached(body, wait.Entry, walk).ToArray();

        foreach (var (effect, stopping, required) in reached)
        {
            if (effect is null) continue;

            if (byEffect.TryGetValue(effect, out var held))
            {
                held.Stopping.IntersectWith(stopping);
                held.Required.IntersectWith(required);
            }
            else
            {
                byEffect[effect] = ([.. stopping], [.. required]);
            }
        }

        if (byEffect.Count == 0 && reached.Length > 0)
        {
            Unread.Add($"{walk.Cdo.Owner?.Name} applies a spec from offset {wait.Entry} with no maker on the path");
        }

        var groups = byEffect
            .Select(entry => (Effect: effectSource(entry.Key), Stopping: Sorted(entry.Value.Stopping), Required: Sorted(entry.Value.Required)))
            .Where(entry => entry.Effect is not null)
            .GroupBy(entry => $"{string.Join(',', entry.Stopping)}|{string.Join(',', entry.Required)}", StringComparer.Ordinal)
            .ToArray();

        if (groups.Length == 0) yield return new CommitTriggerSource(wait.Tags, wait.AllTags, wait.Excluded, [], [], []);

        foreach (var group in groups)
        {
            yield return new CommitTriggerSource(
                wait.Tags,
                wait.AllTags,
                wait.Excluded,
                [.. group.Select(entry => entry.Effect!).OrderBy(effect => effect.NetId)],
                group.First().Stopping,
                group.First().Required);
        }
    }

    /// <summary>
    /// The effects a body applies from an entry offset, through the Blueprint's own functions and events. A function
    /// entered twice gives its first reading, so a cycle of calls ends.
    /// </summary>
    private List<Reach> Reached(UStruct body, int entry, Walk walk)
    {
        if (walk.Reached.TryGetValue((body.Name, entry), out var known)) return known;

        var found = walk.Reached[(body.Name, entry)] = [];
        var paths = walk.PathsOf(body);

        foreach (var (statement, taken) in paths.From(entry, condition => Test(condition, paths).Call is not null))
        {
            var applied = Applied(statement, paths, walk.Cdo).ToArray();
            var called = Called(statement, walk).SelectMany(call => Reached(call.Body, call.Entry, walk).ToArray()).ToArray();

            if (applied.Length == 0 && called.Length == 0) continue;

            var stopping = Tested(taken, paths, walk.Cdo, held: false);
            var required = Tested(taken, paths, walk.Cdo, held: true);

            found.AddRange(applied.Select(effect => new Reach(effect, stopping, required)));
            found.AddRange(called.Select(reach => new Reach(reach.Effect, [.. stopping, .. reach.Stopping], [.. required, .. reach.Required])));
        }

        if (paths.Truncated) Unread.Add($"{walk.Cdo.Owner?.Name} has more paths from offset {entry} than the walk follows");

        return found;
    }

    /// <summary>The Blueprint's own functions a statement calls, and the ubergraph offsets its events enter.</summary>
    private static IEnumerable<(UStruct Body, int Entry)> Called(KismetExpression statement, Walk walk)
    {
        // A delegate bound past a commit runs on that commit, as with a wait on the ability ending. A further commit wait
        // is a trigger of its own.
        if (statement is EX_BindDelegate { FunctionName.Text: var bound } &&
            !bound.StartsWith(CommitDelegate, StringComparison.Ordinal) &&
            !bound.StartsWith(EventDelegate, StringComparison.Ordinal) &&
            walk.Bodies.FirstOrDefault(body => body.Name.Equals(bound, StringComparison.Ordinal)) is { } delegated)
        {
            yield return (delegated, delegated.ScriptBytecode![0].StatementIndex);
        }

        foreach (var call in Invocations(statement, 0))
        {
            if (call is not (EX_LocalFinalFunction or EX_LocalVirtualFunction) || RowSinks.Calling(call) is not { } called) continue;

            if (UbergraphOffset(call) is { } offset)
            {
                if (UbergraphOf(walk.Bodies) is { } ubergraph) yield return (ubergraph, offset);

                continue;
            }

            if (walk.Bodies.FirstOrDefault(body => body.Name.Equals(called, StringComparison.Ordinal)) is { } function)
            {
                yield return (function, function.ScriptBytecode![0].StatementIndex);
            }
        }
    }

    private static string[] Sorted(IEnumerable<string> tags) => [.. tags.Order(StringComparer.Ordinal)];

    /// <summary>The tag test a condition comes to through any negation, and whether the test holds when the condition does.</summary>
    private static (KismetExpression? Call, bool Positive) Test(KismetExpression? condition, BranchPaths paths)
    {
        var positive = true;
        var value = paths.Value(condition);

        while (value is not null && RowSinks.Invoking(value) is { } inverted && RowSinks.Calling(inverted) == Negation)
        {
            positive = !positive;
            value = paths.Value(RowSinks.Parameters(inverted).FirstOrDefault());
        }

        return value is not null && RowSinks.Invoking(value) is { } call && RowSinks.Calling(call) == TagTest
            ? (call, positive)
            : (null, positive);
    }

    private static HashSet<string> Tested(IEnumerable<BranchTaken> taken, BranchPaths paths, UObject cdo, bool held)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        foreach (var branch in taken)
        {
            var (call, positive) = Test(branch.Condition, paths);

            if (call is null || (positive == branch.Held) != held) continue;
            if (SetByCallerDurations.Tag(RowSinks.Parameters(call).FirstOrDefault(), cdo) is { Length: > 0 } tag) tags.Add(tag);
        }

        return tags;
    }

    private IEnumerable<string?> Applied(KismetExpression statement, BranchPaths paths, UObject cdo)
    {
        foreach (var call in Invocations(statement, 0))
        {
            if (RowSinks.Calling(call) is not { } called) continue;
            if (!called.Contains(ApplyingCall, StringComparison.Ordinal) && !called.Contains(SpecCall, StringComparison.Ordinal)) continue;

            var named = RowSinks.Parameters(call).SelectMany(parameter => Effects(parameter, paths, cdo)).ToArray();

            foreach (var effect in named) yield return effect;

            if (named.Length > 0) continue;

            if (called.Contains(ApplyingCall, StringComparison.Ordinal) && called.Contains(SpecCall, StringComparison.Ordinal))
            {
                yield return null;
            }
            else if (called.Contains(ApplyingCall, StringComparison.Ordinal) || called.Contains(MakingCall, StringComparison.Ordinal))
            {
                Unread.Add($"{cdo.Owner?.Name} offset {statement.StatementIndex} calls {called} on an effect the reader cannot find");
            }
        }
    }

    /// <summary>The effects an argument names: a class, or each element of the map or array a local was read from.</summary>
    private IEnumerable<string> Effects(KismetExpression? parameter, BranchPaths paths, UObject cdo)
    {
        var value = paths.Value(parameter);

        if (Effect(value, cdo) is { } effect) yield return effect;

        foreach (var container in paths.Containers(value))
        {
            if (paths.Value(container) is not EX_InstanceVariable variable || DynamicTextFloats.Name(variable) is not { } property) continue;

            IEnumerable<FPropertyTagType?> elements = ClassDefaults.Inherited(cdo, property, provider)?.GenericValue switch
            {
                UScriptMap map => map.Properties.Values,
                UScriptArray array => array.Properties,
                _ => [],
            };

            foreach (var element in elements)
            {
                if (EffectNamed(PropertyWalk.AsString(element?.GenericValue)) is { } held) yield return held;
            }
        }
    }

    private string? Effect(KismetExpression? expression, UObject cdo) => EffectNamed(expression switch
    {
        EX_ObjectConst constant => PropertyWalk.AsString(constant.Value),
        EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } property =>
            PropertyWalk.AsString(ClassDefaults.Inherited(cdo, property, provider)?.GenericValue),
        _ => null,
    });

    private string? EffectNamed(string? path)
    {
        if (path is null || !path.Contains('/')) return null;

        var name = AbilityPresentationIndex.AssetName(path);

        if (name.EndsWith("_C", StringComparison.Ordinal)) name = name[..^2];

        return effects.Contains(name) ? name : null;
    }

    private IEnumerable<Wait> Waited(UStruct body, IReadOnlyList<UStruct> bodies, UObject cdo)
    {
        var statements = body.ScriptBytecode!;

        for (var at = 0; at < statements.Length; at++)
        {
            if (statements[at] is not EX_LetObj { Variable: { } variable, Assignment: { } assigned }) continue;
            if (RowSinks.Invoking(assigned) is not { } call || RowSinks.Calling(call) is not { } called) continue;
            if (called is not (WaitForCommit or WaitForQuery or WaitForEvents or WaitForEvent)) continue;

            var parameter = RowSinks.Parameters(call).ElementAtOrDefault(TagParameter);
            var (tags, all, excluded) = called == WaitForQuery ? Queried(parameter, cdo) : (Named(parameter), false, []);

            if (called is WaitForEvents or WaitForEvent)
            {
                if (!tags.Contains(CommittedEvent, StringComparer.Ordinal)) continue;

                tags = [CommittedEvent];
            }

            var delegated = called is WaitForEvents or WaitForEvent ? EventDelegate : CommitDelegate;

            if (Entry(statements, at, DynamicTextFloats.Name(variable), delegated, bodies) is { } entry)
            {
                yield return new Wait(tags, all, excluded, entry);
            }
            else
            {
                Unread.Add($"{cdo.Owner?.Name}:{body.Name} offset {statements[at].StatementIndex} binds no delegate the reader can follow");
            }
        }
    }

    /// <summary>The ubergraph offset of the function bound to the wait's delegate.</summary>
    private static int? Entry(KismetExpression[] statements, int wait, string? task, string name, IReadOnlyList<UStruct> bodies)
    {
        for (var at = wait + 1; at < statements.Length; at++)
        {
            if (statements[at] is not EX_AddMulticastDelegate { Delegate: EX_Context bound, DelegateToAdd: { } added }) continue;
            if (DynamicTextFloats.Name(bound.ObjectExpression) != task) continue;
            if (DynamicTextFloats.Name(bound.ContextExpression) != name) continue;

            var local = DynamicTextFloats.Name(added);

            for (var back = at - 1; back > wait; back--)
            {
                if (statements[back] is not EX_BindDelegate delegated || DynamicTextFloats.Name(delegated.Delegate) != local) continue;

                var function = delegated.FunctionName.Text;

                return bodies.FirstOrDefault(body => body.Name.Equals(function, StringComparison.Ordinal)) is { } handler
                    ? handler.ScriptBytecode!.Select(UbergraphOffset).FirstOrDefault(offset => offset is not null)
                    : null;
            }
        }

        return null;
    }

    public static int? UbergraphOffset(KismetExpression statement) =>
        RowSinks.Invoking(statement) is { } call &&
        RowSinks.Calling(call)?.StartsWith(Ubergraph, StringComparison.Ordinal) is true &&
        RowSinks.Parameters(call).FirstOrDefault() is EX_IntConst offset
            ? offset.Value
            : null;

    /// <summary>
    /// The tags of a tag query, from its token stream: a version, a flag for a root expression, then expressions. A tag
    /// expression is its type, a count and that many indices into the tag dictionary. An expression of expressions is
    /// its type, a count and those expressions. The build has any-of, and all-of an any-of or all-of and a none-of. A Blueprint
    /// passes the query as a literal or as a variable holding it on the default object.
    /// </summary>
    private (string[] Tags, bool AllTags, string[] Excluded) Queried(KismetExpression? query, UObject cdo)
    {
        if (query is EX_StructConst { Properties: var properties } &&
            properties.OfType<EX_ArrayConst>().ToArray() is [var dictionary, var stream])
        {
            return Decoded(TagElements(dictionary), [.. stream.Elements.OfType<EX_ByteConst>().Select(token => (int)token.Value)]);
        }

        if (query is EX_InstanceVariable && DynamicTextFloats.Name(query) is { } property &&
            ClassDefaults.Inherited(cdo, property, provider)?.GenericValue is FScriptStruct { StructType: IPropertyHolder held } &&
            PropertyWalk.Get(held, DictionaryField)?.GenericValue is UScriptArray named &&
            PropertyWalk.Get(held, TokenStreamField)?.GenericValue is UScriptArray tokens)
        {
            return Decoded(
                [.. named.Properties.Select(tag => PropertyWalk.AsString(tag?.GenericValue) ?? string.Empty)],
                [.. tokens.Properties.Select(token => (int)(PropertyWalk.AsNumber(token?.GenericValue) ?? 0))]);
        }

        Unread.Add($"{cdo.Owner?.Name} passes a tag query the reader cannot find");

        return ([], false, []);
    }

    private (string[] Tags, bool AllTags, string[] Excluded) Decoded(string[] tags, int[] tokens)
    {
        if (tags.Length == 0) return ([], false, []);

        var at = RootOffset;

        if (tokens.Length > at + 1 && tokens[at] == AllExpressions)
        {
            var count = tokens[at + 1];
            (string[] Named, bool All, string[] None) both = ([], false, []);

            at += 2;

            for (var read = 0; read < count; read++)
            {
                if (TagExpression(tokens, ref at, tags) is not var (type, named))
                {
                    Unread.Add($"tag query not read: {string.Join(", ", tags)}");

                    return ([], false, []);
                }

                if (type == NoTags) both.None = named;
                else both = (named, type == AllTags && named.Length > 1, both.None);
            }

            return both;
        }

        if (TagExpression(tokens, ref at, tags) is { } root && root.Type != NoTags) return (root.Tags, root.Type == AllTags && root.Tags.Length > 1, []);

        Unread.Add($"tag query not read: {string.Join(", ", tags)}");

        return ([], false, []);
    }

    private static string[] TagElements(EX_ArrayConst dictionary) =>
        [.. dictionary.Elements.Select(element => Named(element).FirstOrDefault() ?? string.Empty)];

    private static (int Type, string[] Tags)? TagExpression(int[] tokens, ref int at, string[] tags)
    {
        if (at + 1 >= tokens.Length) return null;

        var type = tokens[at];
        var count = tokens[at + 1];

        if (type is not (AnyTags or AllTags or NoTags) || at + 2 + count > tokens.Length) return null;

        var named = tokens[(at + 2)..(at + 2 + count)].Select(index => tags[index]).ToArray();

        at += 2 + count;

        return (type, named);
    }

    private static string[] Named(KismetExpression? expression)
    {
        var names = new List<string>();

        Collect(expression, names, 0);

        return [.. names.Where(name => name.Length > 0 && name != NoTag).Distinct(StringComparer.Ordinal)];
    }

    private static void Collect(KismetExpression? expression, List<string> names, int depth)
    {
        if (depth > MaxDepth) return;

        switch (expression)
        {
            case EX_NameConst name:
                names.Add(name.Value.Text);
                break;
            case EX_StructConst constant:
                foreach (var property in constant.Properties) Collect(property, names, depth + 1);
                break;
            case EX_ArrayConst array:
                foreach (var element in array.Elements) Collect(element, names, depth + 1);
                break;
        }
    }

    internal static IEnumerable<KismetExpression> Invocations(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) yield break;

        if (RowSinks.Invoking(expression) is { } call)
        {
            yield return call;

            foreach (var parameter in RowSinks.Parameters(call))
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
}
