using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed record RowSink(string Property, string? Owner, string Consumer, IReadOnlyList<string> Steps)
{
    public IReadOnlyList<string> Gates { get; init; } = [];
}

public static class RowSinks
{
    private const string ScalableRead = "GetScalableFloat";
    private const string Select = "SelectFloat";
    public const string PropertyUse = "property:";
    public const string SinkUse = "sink:";
    public const string PrintedUse = "printed:";
    public const string VariableUse = "variable:";
    public const string AttributeUse = "attribute:";
    public const string GateMark = "~gate:";
    public const string ClassMark = "class=";
    public const string AssetTagMark = "assettag=";
    public const string GrantedTagMark = "grantedtag=";
    public const string TagMark = "tag=";
    public const string ActorMark = "actor=";
    public const string GranterMark = "granter=self";
    private const string GrantedHandle = "GetGrantedByActiveEffectHandle";
    public const string ChanceGate = "chance@";
    public const string StacksGate = "stacks@";
    public const string EventGate = "event@";
    public const string PerGate = "per@";
    public const string Execution = "execution";
    public const string Holder = "holder";
    private const string ScriptRoot = "/Script/";
    private const string Returned = "ReturnValue";
    private const string ReturnConsumer = "return:";
    private const string SetConsumer = "set:";
    private const string ClassSuffix = "_C";
    private const string ApplyEffect = "ApplyGameplayEffect";
    private const string ProcHandler = "ProcHandler";
    private const string TagNameField = "TagName";
    private const string Spawning = "Spawn";
    private const string Negation = "Not_PreBool";
    private const string Conjunction = "BooleanAND";
    private const string Unequal = "NotEqual_";
    private const string Equal = "EqualEqual_";
    private const string TagToken = "Tag";
    private const string AssetTagToken = "AssetTag";
    private const string GrantedTagToken = "GrantedTag";
    private const int MaxDepth = 40;
    private const int MarkDepth = 6;
    private const string OutPrefix = "CallFunc_";
    private const int TraceDepth = 16;
    private const int SpawnDepth = 2;

    private static readonly string[] Rolls = ["ControlledRandomBool", "RandomBoolWithWeight"];
    private static readonly string[] Thresholds = ["GreaterEqual_", "Greater_"];


    private static readonly IReadOnlySet<string> Unvisited = new HashSet<string>(StringComparer.Ordinal);

    private static readonly string[] Arithmetic =
    [
        "Add_", "Subtract_", "Multiply_", "Divide_", "SelectFloat", "FMax", "FMin", "Conv_", "FTrunc", "FCeil",
        "FFloor", "Round", "SafeDivide", "Abs", "MultiplyMultiply_", "FClamp", "Clamp", "Max", "Min",
    ];

    public static IReadOnlyList<(string Effect, int Stacks, IReadOnlyList<string> Gates)> StackGrants(UObject? asset)
    {
        var grants = new List<(string Effect, int Stacks, IReadOnlyList<string> Gates)>();
        if (asset?.Owner is not { } package) return grants;

        var context = new Context(asset);

        foreach (var export in package.GetExports())
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);
            foreach (var statement in bytecode)
            {
                if (Assigned(statement) is not var (variable, assigned) || variable is EX_InstanceVariable) continue;
                if (DynamicTextFloats.Name(variable) is not { } name) continue;

                if (!locals.TryGetValue(name, out var values)) locals[name] = values = [];
                values.Add(assigned);
            }

            var producers = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in bytecode) Produce(statement, locals, producers, 0);

            var walk = new Walk(body.Name, locals, producers, context, new HashSet<RowSink>(new SinkComparer()));
            var controlled = walk.Controlled(body);

            foreach (var statement in bytecode)
            {
                var call = Invoking(statement) ?? (Assigned(statement) is var (_, value) ? Invoking(value) : null);
                if (call is null || Calling(call) is not { } called || !called.Contains(ApplyEffect, StringComparison.Ordinal)) continue;

                var parameters = Parameters(call);
                if (parameters.Count < 2 || parameters[^1] is not EX_IntConst { Value: > 1 } stacks) continue;

                var effect = parameters
                    .OfType<EX_ObjectConst>()
                    .Select(constant => constant.Value.Name)
                    .FirstOrDefault(name => name?.EndsWith(ClassSuffix, StringComparison.Ordinal) == true);

                if (effect is null) continue;

                grants.Add((effect[..^ClassSuffix.Length], stacks.Value, controlled.GetValueOrDefault(statement.StatementIndex) ?? []));
            }
        }

        return grants;
    }

    public static IReadOnlyList<RowSink> Of(UObject? asset, Func<string, UObject?>? spawned = null) =>
        Of(asset, spawned, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);

    private static IReadOnlyList<RowSink> Of(
        UObject? asset, Func<string, UObject?>? spawned, ISet<string> walked, int depth)
    {
        if (asset?.Owner is not { } package || !walked.Add(package.Name)) return [];

        var sinks = new HashSet<RowSink>(new SinkComparer());
        var context = new Context(asset);
        var spawns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var export in package.GetExports())
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;

            var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in bytecode)
            {
                if (Assigned(statement) is not var (variable, value)) continue;
                if (variable is EX_InstanceVariable) continue;
                if (DynamicTextFloats.Name(variable) is not { } name) continue;

                if (!locals.TryGetValue(name, out var values)) locals[name] = values = [];
                values.Add(value);
            }

            var producers = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in bytecode) Produce(statement, locals, producers, 0);

            var walk = new Walk(body.Name, locals, producers, context, sinks);
            var controlled = walk.Controlled(body);

            foreach (var statement in bytecode)
            {
                var gates = controlled.GetValueOrDefault(statement.StatementIndex);

                foreach (var path in Spawned(statement, 0)) spawns.Add(path);

                if (Assigned(statement) is var (variable, value))
                {
                    if (variable is EX_InstanceVariable && DynamicTextFloats.Name(variable) is { } property)
                    {
                        walk.Visit(value, SetConsumer + property + walk.Readers(property, package), [], 0, Unvisited, gates);
                    }
                    else if (variable is EX_LocalOutVariable || DynamicTextFloats.Name(variable) == Returned)
                    {
                        walk.Visit(value, ReturnConsumer + body.Name, [], 0, Unvisited, gates);
                    }
                    else if (Invoking(value) is { } call && Calling(call) is { } called && !IsArithmetic(called))
                    {
                        walk.Visit(value, null, [], 0, Unvisited, gates);
                    }

                    continue;
                }

                walk.Visit(statement, null, [], 0, Unvisited, gates);
            }
        }

        if (spawned is not null && depth < SpawnDepth)
        {
            foreach (var path in spawns)
            {
                foreach (var sink in Of(spawned(path), spawned, walked, depth + 1)) sinks.Add(sink);
            }
        }

        return [.. sinks];
    }

    public static IReadOnlyList<string> SpawnedActors(UObject? asset)
    {
        var spawned = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var export in asset?.Owner?.GetExports() ?? [])
        {
            if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

            foreach (var statement in bytecode)
            {
                foreach (var path in Spawned(statement, 0)) spawned.Add(Leaf(path));
            }
        }

        return [.. spawned];
    }

    public static IReadOnlyDictionary<string, SortedSet<string>> Rows(
        IReadOnlyList<RowSink> sinks, IEnumerable<CurveBinding> bindings)
    {
        var rows = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var bound = bindings.ToArray();

        foreach (var sink in sinks)
        {
            var resolved = sink with { Gates = [.. sink.Gates.Select(gate => Resolved(gate, bound)).OfType<string>()] };

            foreach (var binding in bound)
            {
                if (!Binds(binding, sink.Property)) continue;
                if (!rows.TryGetValue(binding.RowName, out var uses)) rows[binding.RowName] = uses = new(StringComparer.Ordinal);

                uses.Add(Describe(resolved));
            }
        }

        return rows;
    }

    private static string? Resolved(string gate, IReadOnlyList<CurveBinding> bindings)
    {
        CurveBinding? Bound(string property) =>
            bindings.FirstOrDefault(binding => Binds(binding, property) && binding.Value is not null);

        string? Value(string property) =>
            Bound(property)?.Value is { } value
                ? value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                : null;

        if (gate.StartsWith(ChanceGate, StringComparison.Ordinal))
        {
            var property = gate[ChanceGate.Length..];
            return Value(property) is { } chance ? $"{ChanceGate}{chance}@{Bound(property)!.RowName}" : null;
        }

        if (gate.StartsWith(StacksGate, StringComparison.Ordinal))
        {
            var parts = gate[StacksGate.Length..].Split('@');
            return parts.Length == 2 && Value(parts[0]) is { } stacks ? $"{StacksGate}{stacks}@{parts[1]}" : null;
        }

        return gate;
    }

    public static IReadOnlySet<string> Variables(UObject? asset)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var type = asset?.Class?.Load() as UStruct;

        for (var depth = 0; type is not null && depth < MaxDepth; depth++)
        {
            if (type.GetPathName().StartsWith(ScriptRoot, StringComparison.Ordinal)) break;

            foreach (var child in type.ChildProperties ?? []) names.Add(child.Name.Text);

            type = type.SuperStruct?.Load<UStruct>();
        }

        return names;
    }

    public static string Head(string property)
    {
        var dot = property.IndexOf('.');
        var head = dot < 0 ? property : property[..dot];
        var index = head.IndexOf('[');

        return index < 0 ? head : head[..index];
    }

    public static string Describe(RowSink sink) =>
        (sink.Steps.Count == 0 ? sink.Consumer : $"{sink.Consumer}|{string.Join(",", sink.Steps)}") +
        (sink.Gates.Count == 0 ? string.Empty : GateMark + string.Join(";", sink.Gates.Distinct(StringComparer.Ordinal)));

    public static bool Binds(CurveBinding binding, string property) =>
        binding.Property.Equals(property, StringComparison.OrdinalIgnoreCase) ||
        HeroNames.LastSegment(binding.Property).Equals(property, StringComparison.OrdinalIgnoreCase) ||
        binding.Property.StartsWith(property + ".", StringComparison.OrdinalIgnoreCase);

    public static string? OwnerClass(string? owner) =>
        owner is null ? null : owner.EndsWith(ClassSuffix, StringComparison.Ordinal) ? owner[..^ClassSuffix.Length] : owner;

    private sealed class Context
    {
        private readonly UObject _cdo;

        public Context(UObject cdo)
        {
            _cdo = cdo;

            foreach (var export in cdo.Owner?.GetExports() ?? [])
            {
                if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode }) continue;

                foreach (var statement in bytecode) Scan(statement, 0);
            }

            foreach (var export in cdo.Owner?.GetExports() ?? [])
            {
                if (export is not UStruct { ScriptBytecode: { Length: > 0 } bytecode } body) continue;
                if (BytecodeGates.Of(body, cdo) is not { } graph) continue;

                var raised = new Dictionary<int, KismetExpression>();

                foreach (var statement in bytecode)
                {
                    if (statement is EX_LetBool { Variable: EX_InstanceVariable, Assignment: EX_True assigned }) raised[statement.StatementIndex] = assigned;
                }

                if (raised.Count == 0) continue;

                foreach (var (call, reached, _) in CombatEvents.Bound(body, cdo, graph, called => called.Contains(ProcHandler, StringComparison.Ordinal)))
                {
                    if (Literal(Parameters(call).ElementAtOrDefault(2)) is not { } tag) continue;

                    foreach (var at in reached)
                    {
                        if (!raised.TryGetValue(at, out var assigned)) continue;
                        if (!FlagProcs.TryGetValue(assigned, out var procs)) FlagProcs[assigned] = procs = [];
                        if (!procs.Contains(tag)) procs.Add(tag);
                    }
                }
            }
        }

        public UObject Cdo => _cdo;

        public Dictionary<KismetExpression, List<string>> FlagProcs { get; } = new(ReferenceEqualityComparer.Instance);

        public List<string> ProcTags { get; } = [];

        public Dictionary<string, List<KismetExpression>> Flags { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<KismetExpression>> Assignments { get; } = new(StringComparer.Ordinal);

        public string? Value(string property)
        {
            var value = PropertyWalk.Get(_cdo, property)?.GenericValue;
            if (value is FScriptStruct { StructType: { } inner }) value = inner;

            return value switch
            {
                FGameplayTag or FPackageIndex or FSoftObjectPath => PropertyWalk.AsString(value),
                IPropertyHolder holder when PropertyWalk.Get(holder, TagNameField) is { } named =>
                    PropertyWalk.AsString(named.GenericValue),
                _ => null,
            };
        }

        private void Scan(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MarkDepth) return;

            if (expression is EX_LetBool { Variable: EX_InstanceVariable variable, Assignment: { } assigned } &&
                DynamicTextFloats.Name(variable) is { } flag)
            {
                if (!Flags.TryGetValue(flag, out var values)) Flags[flag] = values = [];
                values.Add(assigned);
            }

            if (Assigned(expression) is var (target, value) && target is EX_InstanceVariable &&
                DynamicTextFloats.Name(target) is { } member)
            {
                if (!Assignments.TryGetValue(member, out var written)) Assignments[member] = written = [];
                written.Add(value);
            }

            if (Invoking(expression) is { } call && Calling(call) is { } called &&
                called.Contains(ProcHandler, StringComparison.Ordinal) &&
                Parameters(call).ElementAtOrDefault(2) is { } tagged &&
                Literal(tagged) is { } tag)
            {
                ProcTags.Add(tag);
            }

            foreach (var child in Invoking(expression) is { } invoked ? Parameters(invoked) : Children(expression))
            {
                Scan(child, depth + 1);
            }
        }

        public string? Literal(KismetExpression? expression) => expression switch
        {
            EX_NameConst name when !name.Value.IsNone && name.Value.Text.Contains('.') => name.Value.Text,
            EX_StringConst text when text.Value.Contains('.') && !text.Value.Contains('/') => text.Value,
            EX_StructConst constant => constant.Properties.Select(Literal).FirstOrDefault(found => found is not null),
            EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property &&
                                              Value(property) is { } held && held.Contains('.') && !held.Contains('/') =>
                held,
            EX_Context context => Literal(context.ContextExpression),
            _ => null,
        };
    }

    private sealed class Walk(
        string function,
        IReadOnlyDictionary<string, List<KismetExpression>> locals,
        IReadOnlyDictionary<string, List<KismetExpression>> producers,
        Context context,
        ISet<RowSink> sinks)
    {
        public void Visit(
            KismetExpression? expression,
            string? consumer,
            IReadOnlyList<string> steps,
            int depth,
            IReadOnlySet<string> visiting,
            IReadOnlyList<string>? gates = null)
        {
            if (expression is null || depth > MaxDepth) return;

            switch (expression)
            {
                case EX_LocalVariable or EX_LocalOutVariable:
                {
                    if (DynamicTextFloats.Name(expression) is not { } name) return;
                    if (visiting.Contains(name) || !locals.TryGetValue(name, out var values)) return;

                    var deeper = new HashSet<string>(visiting, StringComparer.Ordinal) { name };
                    foreach (var value in values) Visit(value, consumer, steps, depth + 1, deeper, gates);

                    return;
                }
                case EX_Cast cast:
                    Visit(cast.Target, consumer, steps, depth + 1, visiting, gates);
                    return;
            }

            if (Invoking(expression) is { } call && Calling(call) is { } called)
            {
                var parameters = Parameters(call);

                if (called.StartsWith(ScalableRead, StringComparison.Ordinal))
                {
                    if (Read(parameters.FirstOrDefault()) is var (property, owner))
                    {
                        sinks.Add(new RowSink(property, owner, consumer ?? ReturnConsumer + function, steps)
                        {
                            Gates = gates ?? [],
                        });
                    }

                    return;
                }

                if (IsArithmetic(called))
                {
                    for (var at = 0; at < parameters.Count; at++)
                    {
                        IReadOnlyList<string>? gated = called.StartsWith(Select, StringComparison.Ordinal) && at == 0
                            ? [.. gates ?? [], .. States(parameters.ElementAtOrDefault(2), true, 0, visiting)]
                            : gates;

                        Visit(parameters[at], consumer, [.. steps, Step(called, at, parameters)], depth + 1, visiting, gated);
                    }

                    return;
                }

                var qualifier = Qualifier(expression is EX_Context { ObjectExpression: { } target } ? [.. parameters, target] : parameters);

                for (var at = 0; at < parameters.Count; at++)
                {
                    Visit(parameters[at], $"{called}#{at}{qualifier}", [], depth + 1, visiting, gates);
                }

                if (expression is EX_Context outer)
                {
                    Visit(outer.ObjectExpression, consumer, steps, depth + 1, visiting, gates);
                }

                return;
            }

            foreach (var child in Children(expression)) Visit(child, consumer, steps, depth + 1, visiting, gates);
        }

        public Dictionary<int, IReadOnlyList<string>> Controlled(UStruct body)
        {
            var controlled = new Dictionary<int, IReadOnlyList<string>>();

            if (BytecodeGates.Of(body, context.Cdo) is not { } graph) return controlled;

            void Add(IEnumerable<int> statements, IReadOnlyList<string> states)
            {
                if (states.Count == 0) return;

                foreach (var at in statements)
                {
                    controlled[at] = controlled.TryGetValue(at, out var held) ? [.. held, .. states] : states;
                }
            }

            var code = body.ScriptBytecode ?? [];

            foreach (var (guard, at) in graph.Branches)
            {
                var condition = Reaching(code, guard, at);

                Add(graph.Only(at), [.. States(condition, true, 0, Unvisited)]);
                Add(graph.OnlyJumped(at), [.. States(condition, false, 0, Unvisited)]);
            }

            foreach (var (at, events) in CombatEvents.Of(body, context.Cdo, graph))
            {
                Add([at], [.. events.Select(tag => PerGate + tag)]);
            }

            return controlled;
        }

        private static KismetExpression Reaching(KismetExpression[] code, KismetExpression condition, int at)
        {
            if (condition is not (EX_LocalVariable or EX_LocalOutVariable) || DynamicTextFloats.Name(condition) is not { } name)
            {
                return condition;
            }

            for (var back = at - 1; back >= 0; back--)
            {
                if (Assigned(code[back]) is var (variable, value) && DynamicTextFloats.Name(variable) == name) return value;
            }

            return condition;
        }

        private IEnumerable<string> States(
            KismetExpression? expression, bool positive, int depth, IReadOnlySet<string> visiting)
        {
            if (expression is null || depth > MarkDepth) yield break;

            switch (expression)
            {
                case EX_Cast cast:
                    foreach (var state in States(cast.Target, positive, depth + 1, visiting)) yield return state;
                    yield break;
                case EX_LocalVariable or EX_LocalOutVariable:
                {
                    if (DynamicTextFloats.Name(expression) is not { } name || visiting.Contains(name)) yield break;

                    var deeper = new HashSet<string>(visiting, StringComparer.Ordinal) { name };

                    foreach (var value in Sources(name))
                    {
                        foreach (var state in States(value, positive, depth + 1, deeper)) yield return state;
                    }

                    yield break;
                }
                case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } flag:
                {
                    if (!positive) yield break;

                    if (context.Literal(variable) is { } tag)
                    {
                        yield return tag;
                        yield break;
                    }

                    if (visiting.Contains(flag) || !context.Flags.TryGetValue(flag, out var assigned)) yield break;

                    var deeper = new HashSet<string>(visiting, StringComparer.Ordinal) { flag };

                    foreach (var value in assigned)
                    {
                        if (value is EX_True)
                        {
                            foreach (var proc in (context.FlagProcs.GetValueOrDefault(value) ?? context.ProcTags).Distinct(StringComparer.Ordinal))
                            {
                                yield return proc;
                            }

                            continue;
                        }

                        foreach (var state in States(value, true, depth + 1, deeper)) yield return state;
                    }

                    yield break;
                }
                case EX_NameConst name when positive && !name.Value.IsNone && name.Value.Text.Contains('.'):
                    yield return name.Value.Text;
                    yield break;
            }

            if (Invoking(expression) is not { } call || Calling(call) is not { } called) yield break;

            var parameters = Parameters(call);

            if (called.StartsWith(Negation, StringComparison.Ordinal))
            {
                foreach (var state in States(parameters.FirstOrDefault(), !positive, depth + 1, visiting)) yield return state;
                yield break;
            }

            if (called.StartsWith(Conjunction, StringComparison.Ordinal))
            {
                if (!positive) yield break;

                foreach (var parameter in parameters)
                {
                    foreach (var state in States(parameter, true, depth + 1, visiting)) yield return state;
                }

                yield break;
            }

            if (Array.Exists(Rolls, roll => called.StartsWith(roll, StringComparison.Ordinal)))
            {
                if (!positive) yield break;

                foreach (var read in parameters.Select(parameter => Reading(parameter, 0)).OfType<string>())
                {
                    yield return ChanceGate + read;
                }

                yield break;
            }

            if (Array.Exists(Thresholds, threshold => called.StartsWith(threshold, StringComparison.Ordinal)) &&
                parameters.Count == 2)
            {
                if (!positive) yield break;

                var counted = Marks(parameters[0], null, 0, visiting)
                    .Select(Tagged)
                    .OfType<string>()
                    .FirstOrDefault();

                if (counted is not null && Reading(parameters[1], 0) is { } threshold)
                {
                    yield return $"{StacksGate}{threshold}@{counted}";
                }

                yield break;
            }

            if (called.StartsWith(Unequal, StringComparison.Ordinal) || called.StartsWith(Equal, StringComparison.Ordinal))
            {
                if (!called.Contains(TagToken, StringComparison.Ordinal)) yield break;

                var equal = called.StartsWith(Equal, StringComparison.Ordinal) ? positive : !positive;
                if (!equal) yield break;

                foreach (var parameter in parameters)
                {
                    if (context.Literal(parameter) is { } tag) yield return EventGate + tag;
                }

                yield break;
            }

            if (!positive || !called.Contains(TagToken, StringComparison.Ordinal)) yield break;

            foreach (var mark in parameters.SelectMany(parameter => Marks(parameter, null, 0, visiting)))
            {
                if (Tagged(mark) is { } tag) yield return tag;
            }
        }

        private static string? Tagged(string mark)
        {
            foreach (var prefix in new[] { AssetTagMark, GrantedTagMark, TagMark })
            {
                if (mark.StartsWith(prefix, StringComparison.Ordinal)) return mark[prefix.Length..];
            }

            return null;
        }

        private string? Reading(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MarkDepth) return null;

            if (Invoking(expression) is { } call && Calling(call) is { } called)
            {
                if (called.StartsWith(ScalableRead, StringComparison.Ordinal))
                {
                    return Read(Parameters(call).FirstOrDefault()) is var (property, _) ? property : null;
                }

                return Parameters(call).Select(parameter => Reading(parameter, depth + 1)).FirstOrDefault(found => found is not null);
            }

            if (expression is EX_LocalVariable or EX_LocalOutVariable && DynamicTextFloats.Name(expression) is { } name)
            {
                return Sources(name).Select(value => Reading(value, depth + 1)).FirstOrDefault(found => found is not null);
            }

            return expression is EX_Cast cast ? Reading(cast.Target, depth + 1) : null;
        }

        private IEnumerable<KismetExpression> Sources(string local) =>
            (locals.GetValueOrDefault(local) ?? []).Concat(producers.GetValueOrDefault(local) ?? []);

        private readonly Dictionary<(string Local, string? Kind), string[]> _traced = [];

        private IEnumerable<KismetExpression> Written(string local) =>
            (locals.GetValueOrDefault(local) ?? []).Concat((producers.GetValueOrDefault(local) ?? [])
                .Where(call => Calling(call) is { } called && local.StartsWith($"{OutPrefix}{called}_", StringComparison.Ordinal)));

        public string Readers(string property, CUE4Parse.UE4.Assets.IPackage package)
        {
            foreach (var export in package.GetExports())
            {
                if (export is not UStruct { ScriptBytecode: { Length: > 0 } code }) continue;

                var aliases = new HashSet<string>(StringComparer.Ordinal);

                foreach (var statement in code)
                {
                    if (Assigned(statement) is not var (variable, value) || variable is EX_InstanceVariable) continue;
                    if ((value is EX_Cast cast ? cast.Target : value) is EX_InstanceVariable read &&
                        DynamicTextFloats.Name(read) == property && DynamicTextFloats.Name(variable) is { } alias)
                    {
                        aliases.Add(alias);
                    }
                }

                foreach (var statement in code)
                {
                    var reached = Invoking(statement) ?? (Assigned(statement) is var (_, assigned) ? Invoking(assigned) : null);
                    if (reached is null || Calling(reached) is not { } called || IsArithmetic(called)) continue;

                    var parameters = Parameters(reached);

                    if (!parameters.Any(parameter =>
                            DynamicTextFloats.Name(parameter) is { } name &&
                            (parameter is EX_InstanceVariable ? name == property : aliases.Contains(name))))
                    {
                        continue;
                    }

                    var marks = parameters
                        .SelectMany(parameter => Marks(parameter, null, 0, Unvisited))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();

                    if (marks.Length > 0) return string.Concat(marks.Select(mark => $"<{mark}>"));
                }
            }

            return string.Empty;
        }

        private string Qualifier(IReadOnlyList<KismetExpression> parameters)
        {
            var name = parameters.Select(Named).FirstOrDefault(found => found is not null);

            var marks = parameters
                .SelectMany(parameter => Marks(parameter, null, 0, Unvisited))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return (name is null ? string.Empty : $"[{name}]") + string.Concat(marks.Select(mark => $"<{mark}>"));
        }

        private IEnumerable<string> Marks(
            KismetExpression? expression, string? kind, int depth, IReadOnlySet<string> visiting)
        {
            if (expression is null || depth > TraceDepth) yield break;

            switch (expression)
            {
                case EX_ObjectConst { Value.Name: { } named } when named.EndsWith(ClassSuffix, StringComparison.Ordinal):
                    yield return ClassMark + named[..^ClassSuffix.Length];
                    yield break;
                case EX_SoftObjectConst soft when Text(soft.Value) is { } path:
                    yield return ClassMark + Leaf(path);
                    yield break;
                case EX_InstanceVariable variable when DynamicTextFloats.Name(variable) is { } property:
                    if (context.Value(property) is { } held)
                    {
                        if (held.Contains('/')) yield return ClassMark + Leaf(held);
                        else if (held.Contains('.')) yield return (kind ?? TagMark) + held;
                        yield break;
                    }

                    if (visiting.Contains(property) || !context.Assignments.TryGetValue(property, out var written)) yield break;

                    var through = new HashSet<string>(visiting, StringComparer.Ordinal) { property };

                    foreach (var value in written)
                    {
                        foreach (var mark in Marks(value, kind, depth + 1, through)) yield return mark;
                    }

                    yield break;
                case EX_DynamicCast cast:
                    if (PropertyWalk.AsString(cast.ClassPtr) is { } actor && actor.Contains('/')) yield return ActorMark + Leaf(actor);
                    foreach (var mark in Marks(cast.Target, kind, depth + 1, visiting)) yield return mark;
                    yield break;
                case EX_NameConst constant when depth > 0 && !constant.Value.IsNone && constant.Value.Text.Contains('.'):
                    yield return (kind ?? TagMark) + constant.Value.Text;
                    yield break;
                case EX_LocalVariable or EX_LocalOutVariable:
                {
                    if (DynamicTextFloats.Name(expression) is not { } local || visiting.Contains(local)) yield break;

                    if (!_traced.TryGetValue((local, kind), out var found))
                    {
                        var deeper = new HashSet<string>(visiting, StringComparer.Ordinal) { local };

                        found = [.. Written(local).SelectMany(value => Marks(value, kind, depth + 1, deeper)).Distinct(StringComparer.Ordinal)];
                        _traced[(local, kind)] = found;
                    }

                    foreach (var mark in found) yield return mark;

                    yield break;
                }
                case EX_Cast cast:
                    foreach (var mark in Marks(cast.Target, kind, depth + 1, visiting)) yield return mark;
                    yield break;
                case EX_Context outer:
                    foreach (var mark in Marks(outer.ContextExpression, kind, depth + 1, visiting)) yield return mark;
                    foreach (var mark in Marks(outer.ObjectExpression, kind, depth + 1, visiting)) yield return mark;
                    yield break;
            }

            if (Invoking(expression) is { } call && Calling(call) is { } called)
            {
                if (called.StartsWith(ScalableRead, StringComparison.Ordinal) || IsArithmetic(called)) yield break;

                if (called.Contains(GrantedHandle, StringComparison.Ordinal))
                {
                    yield return GranterMark;
                    yield break;
                }

                var nested = called.Contains(AssetTagToken, StringComparison.Ordinal) ? AssetTagMark
                    : called.Contains(GrantedTagToken, StringComparison.Ordinal) ? GrantedTagMark
                    : kind;

                foreach (var parameter in Parameters(call))
                {
                    foreach (var mark in Marks(parameter, nested, depth + 1, visiting)) yield return mark;
                }

                yield break;
            }

            foreach (var child in Children(expression))
            {
                foreach (var mark in Marks(child, kind, depth + 1, visiting)) yield return mark;
            }
        }
    }

    private static void Produce(
        KismetExpression? expression,
        IReadOnlyDictionary<string, List<KismetExpression>> locals,
        Dictionary<string, List<KismetExpression>> producers,
        int depth)
    {
        if (expression is null || depth > MarkDepth) return;

        if (Invoking(expression) is { } call)
        {
            foreach (var parameter in Parameters(call))
            {
                if (parameter is not (EX_LocalVariable or EX_LocalOutVariable)) continue;
                if (DynamicTextFloats.Name(parameter) is not { } name || locals.ContainsKey(name)) continue;

                if (!producers.TryGetValue(name, out var made)) producers[name] = made = [];
                if (!made.Contains(call)) made.Add(call);
            }

            foreach (var parameter in Parameters(call)) Produce(parameter, locals, producers, depth + 1);
            return;
        }

        foreach (var child in Children(expression)) Produce(child, locals, producers, depth + 1);
    }

    internal static IEnumerable<string> Spawned(KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MarkDepth) yield break;

        if (Invoking(expression) is { } call && Calling(call) is { } called)
        {
            if (called.Contains(Spawning, StringComparison.Ordinal))
            {
                foreach (var parameter in Parameters(call))
                {
                    if (parameter is EX_ObjectConst constant && PropertyWalk.AsString(constant.Value) is { } path &&
                        path.EndsWith(ClassSuffix, StringComparison.Ordinal))
                    {
                        yield return path;
                    }
                }
            }

            foreach (var parameter in Parameters(call))
            {
                foreach (var path in Spawned(parameter, depth + 1)) yield return path;
            }

            yield break;
        }

        foreach (var child in Children(expression))
        {
            foreach (var path in Spawned(child, depth + 1)) yield return path;
        }
    }

    internal static string Leaf(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot >= 0) name = name[(dot + 1)..];

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }

    private static string? Text(KismetExpression? expression) => expression switch
    {
        EX_StringConst text => text.Value,
        EX_UnicodeStringConst text => text.Value,
        _ => null,
    };

    private static bool IsArithmetic(string called) =>
        Array.Exists(Arithmetic, prefix => called.StartsWith(prefix, StringComparison.Ordinal));

    private static string Step(string called, int at, IReadOnlyList<KismetExpression> parameters)
    {
        var others = parameters
            .Select((parameter, index) => (parameter, index))
            .Where(entry => entry.index != at)
            .Select(entry => Literal(entry.parameter) is { } number ? number.ToString("R") : "_");

        return $"{called}@{at}({string.Join(";", others)})";
    }

    private static double? Literal(KismetExpression? expression) => expression switch
    {
        EX_DoubleConst constant => constant.Value,
        EX_FloatConst constant => constant.Value,
        EX_IntConst constant => constant.Value,
        EX_IntZero => 0,
        EX_IntOne => 1,
        EX_Cast cast => Literal(cast.Target),
        _ => null,
    };

    private static string? Named(KismetExpression? expression) => expression switch
    {
        EX_NameConst name when !name.Value.IsNone => name.Value.Text,
        EX_StructConst constant => constant.Properties.Select(Named).FirstOrDefault(found => found is not null),
        _ => null,
    };

    private static (string Property, string? Owner)? Read(KismetExpression? expression) => expression switch
    {
        EX_InstanceVariable variable when variable.Variable.New?.Path is { Length: > 0 } path =>
            (path[^1].Text, OwnerClass(variable.Variable.New.ResolvedOwner?.Name)),
        EX_StructMemberContext member when member.Property.New?.Path is { Length: > 0 } path =>
            (path[^1].Text, null),
        EX_Context context => Read(context.ContextExpression),
        EX_Cast cast => Read(cast.Target),
        _ => null,
    };

    private static (KismetExpression Variable, KismetExpression Value)? Assigned(KismetExpression statement) =>
        statement switch
        {
            EX_Let let when let.Variable is not null && let.Assignment is not null => (let.Variable, let.Assignment),
            EX_LetObj let when let.Variable is not null && let.Assignment is not null => (let.Variable, let.Assignment),
            EX_LetBool let when let.Variable is not null && let.Assignment is not null => (let.Variable, let.Assignment),
            _ => null,
        };

    internal static KismetExpression? Invoking(KismetExpression? expression) => expression switch
    {
        EX_Context context => Invoking(context.ContextExpression),
        EX_CallMath or EX_LocalFinalFunction or EX_FinalFunction
            or EX_LocalVirtualFunction or EX_VirtualFunction => expression,
        _ => null,
    };

    internal static string? Calling(KismetExpression expression) => expression switch
    {
        EX_CallMath call => call.StackNode.Name,
        EX_LocalFinalFunction call => call.StackNode.Name,
        EX_FinalFunction call => call.StackNode.Name,
        EX_LocalVirtualFunction call => call.VirtualFunctionName.Text,
        EX_VirtualFunction call => call.VirtualFunctionName.Text,
        _ => null,
    };

    internal static IReadOnlyList<KismetExpression> Parameters(KismetExpression expression) => expression switch
    {
        EX_CallMath call => call.Parameters,
        EX_LocalFinalFunction call => call.Parameters,
        EX_FinalFunction call => call.Parameters,
        EX_LocalVirtualFunction call => call.Parameters,
        EX_VirtualFunction call => call.Parameters,
        _ => [],
    };

    internal static IEnumerable<KismetExpression?> Children(KismetExpression expression) => expression switch
    {
        EX_Let let => [let.Variable, let.Assignment],
        EX_LetObj let => [let.Variable, let.Assignment],
        EX_LetBool let => [let.Variable, let.Assignment],
        EX_Context context => [context.ObjectExpression, context.ContextExpression],
        EX_InterfaceContext context => [context.InterfaceValue],
        EX_DynamicCast cast => [cast.Target],
        EX_Return returned => [returned.ReturnExpression],
        EX_JumpIfNot jump => [jump.BooleanExpression],
        EX_PopExecutionFlowIfNot branch => [branch.BooleanExpression],
        EX_StructConst constant => constant.Properties,
        EX_ArrayConst constant => constant.Elements,
        EX_SetArray array => array.Elements.Prepend(array.AssigningProperty),
        EX_SwitchValue chosen => chosen.Cases
            .SelectMany(c => new KismetExpression?[] { c.CaseIndexValueTerm, c.CaseTerm })
            .Prepend(chosen.IndexTerm)
            .Append(chosen.DefaultTerm),
        EX_StructMemberContext member => [member.StructExpression],
        _ => [],
    };

    private sealed class SinkComparer : IEqualityComparer<RowSink>
    {
        public bool Equals(RowSink? x, RowSink? y) =>
            x is not null && y is not null &&
            x.Property == y.Property && x.Owner == y.Owner && x.Consumer == y.Consumer &&
            x.Steps.SequenceEqual(y.Steps) && x.Gates.SequenceEqual(y.Gates);

        public int GetHashCode(RowSink sink) => HashCode.Combine(sink.Property, sink.Owner, sink.Consumer, sink.Steps.Count);
    }
}
