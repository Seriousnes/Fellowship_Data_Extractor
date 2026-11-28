using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Reads the routes of an ability's graph from its activation to <c>K2_EndAbility</c>. A task's delegate, a latent
/// action's linkage and a timer's delegate each enter the graph at an offset, and the statements from that offset start
/// the next tasks. The graph of a parent Blueprint class is read where the ability's own class has none. A statement
/// reached only past a failed <c>K2_CommitAbility</c> is left out. A task and an end have the branches between the
/// entry of their handler and their statement, and a handler starting an earlier task of its route is a loop.
/// </summary>
public sealed class EndAbilityPaths(AbstractFileProvider provider, ConstantsTable constants)
{
    private const string EndCall = "K2_EndAbility";
    private const string CommitCall = "K2_CommitAbility";
    private const string Negation = "Not_PreBool";
    private const string Ubergraph = "ExecuteUbergraph";
    private const string LatentInfo = "LatentActionInfo";
    private const string ScalableRead = "GetScalableFloat";
    private const string Conversion = "Conv_";
    private const string ScaledWait = "WaitDelayCR";
    private const int ScaleParameter = 2;
    private const string Timer = "K2_SetTimerDelegate";
    private const int StartDelayParameter = 4;
    private const int StartDelayVarianceParameter = 5;
    private const int MaxDepth = 24;

    /// <summary>The routes kept for one ability. A graph with more has <see cref="Truncated"/> name it.</summary>
    public const int MaxPaths = 64;

    private static readonly string[] Activations = ["K2_ActivateAbilityFromEvent", "K2_ActivateAbility"];

    /// <summary>
    /// The parameter holding the seconds from a function's call to a delegate of its task: <c>Time</c> of the wait
    /// tasks and the timer, <c>Duration</c> of the latent delays, and <c>LeapTime</c> and <c>Timeout</c> of
    /// <c>CRAbilityTask_AILeapTo</c>. Shipping exe 26320967: the leap's <c>TickTask</c> 0x145117E30 broadcasts
    /// <c>OnArrived</c> once <c>LeapTime</c> (+0x150) has passed since <c>Activate</c>, and a <c>Timeout</c> above 0 sets
    /// a timer for <c>OnTimeout</c> (0x14510F6C0). <c>K2_SetTimerDelegate</c> 0x143ABCF70 first fires
    /// <c>Time + InitialStartDelay</c> after the call (0x143ABD16C), moved by a roll within
    /// <c>InitialStartDelayVariance</c>, so a timer has seconds where its variance is 0.
    /// </summary>
    private static readonly Dictionary<(string Task, string? Delegate), int> Timed = new()
    {
        [(ScaledWait, "OnFinish")] = 1,
        [("WaitDelay", "OnFinish")] = 1,
        [("Delay", null)] = 1,
        [("RetriggerableDelay", null)] = 1,
        [(Timer, null)] = 1,
        [("AILeapToLocation", "OnArrived")] = 2,
        [("AILeapToTarget", "OnArrived")] = 2,
        [("AILeapToLocation", "OnTimeout")] = 4,
        [("AILeapToTarget", "OnTimeout")] = 4,
    };

    /// <summary>The route steps one ability's routes are read within.</summary>
    private const int MaxSteps = 20000;

    /// <summary>Abilities with more routes than <see cref="MaxPaths"/>.</summary>
    public List<string> Truncated { get; } = [];

    /// <summary>Abilities read with a route.</summary>
    public int Abilities { get; private set; }

    /// <summary>Timed waits on the routes read.</summary>
    public int Waits { get; private set; }

    /// <summary>Timed waits with no seconds.</summary>
    public int Unread { get; private set; }

    private sealed record Edge(EndAbilityTask Task, UStruct Body, int Entry, int At);

    private sealed class Segment
    {
        public bool Ends { get; private set; }

        /// <summary>The branches every <c>K2_EndAbility</c> of the segment is reached under.</summary>
        public IReadOnlyList<BranchCondition> EndConditions { get; private set; } = [];

        public List<Edge> Edges { get; } = [];

        public void End(IReadOnlyList<BranchCondition> conditions)
        {
            EndConditions = Ends ? [.. EndConditions.Intersect(conditions)] : conditions;
            Ends = true;
        }

        /// <summary>Adds a task start, once for each statement starting it.</summary>
        public void Start(Edge edge)
        {
            if (!Edges.Any(known => known.Body == edge.Body && known.Entry == edge.Entry && known.At == edge.At &&
                                    known.Task.Task == edge.Task.Task && known.Task.Delegate == edge.Task.Delegate))
            {
                Edges.Add(edge);
            }
        }
    }

    private sealed class Graph(UObject cdo, IReadOnlyList<UStruct> bodies, IReadOnlySet<string> numeric)
    {
        private readonly Dictionary<UStruct, BranchPaths> _paths = [];
        private readonly Dictionary<UStruct, EndAbilityConditions> _conditions = [];
        private readonly Dictionary<UStruct, Dictionary<int, int>> _indexes = [];

        public UObject Cdo => cdo;

        /// <summary>Float and double variables a Blueprint class of the chain declares. A class default without one has 0.</summary>
        public IReadOnlySet<string> Numeric => numeric;

        public Dictionary<(UStruct Body, int Entry), Segment> Segments { get; } = [];

        /// <summary>Instance variables a graph assigns, so their class default is not the value a wait reads.</summary>
        public HashSet<string> Assigned { get; } = AssignedIn(bodies);

        public UStruct? Function(string name) =>
            bodies.FirstOrDefault(body => body.Name.Equals(name, StringComparison.Ordinal));

        public BranchPaths PathsOf(UStruct body) =>
            _paths.TryGetValue(body, out var known) ? known : _paths[body] = new BranchPaths(body);

        /// <summary>The branches of a body, with the side of a failed <c>K2_CommitAbility</c> closed.</summary>
        public EndAbilityConditions ConditionsOf(UStruct body) =>
            _conditions.TryGetValue(body, out var known)
                ? known
                : _conditions[body] = new EndAbilityConditions(
                    body,
                    (branch, held) => Commit(Tested(branch), PathsOf(body)) is { } positive ? positive != held : null);

        public int IndexOf(UStruct body, KismetExpression statement)
        {
            if (!_indexes.TryGetValue(body, out var index))
            {
                index = _indexes[body] = [];

                for (var at = 0; at < body.ScriptBytecode!.Length; at++) index[body.ScriptBytecode[at].StatementIndex] = at;
            }

            return index.GetValueOrDefault(statement.StatementIndex, -1);
        }

        private static HashSet<string> AssignedIn(IReadOnlyList<UStruct> bodies)
        {
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (var statement in bodies.SelectMany(body => body.ScriptBytecode!))
            {
                var variable = statement switch
                {
                    EX_Let let => let.Variable,
                    EX_LetObj let => let.Variable,
                    EX_LetBool let => let.Variable,
                    _ => null,
                };

                if (variable is EX_InstanceVariable && DynamicTextFloats.Name(variable) is { } name) assigned.Add(name);
            }

            return assigned;
        }
    }

    public IReadOnlyList<EndAbilityPath> Of(UObject cdo)
    {
        var bodies = ClassDefaults.Chain(cdo, provider)
            .SelectMany(holder => holder.Owner?.GetExports().OfType<UStruct>() ?? [])
            .Where(body => body.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var numeric = ClassDefaults.Chain(cdo, provider)
            .SelectMany(holder => (holder.Class?.Load() as UStruct)?.ChildProperties ?? [])
            .Where(child => child is FFloatProperty or FDoubleProperty)
            .Select(child => child.Name.Text)
            .ToHashSet(StringComparer.Ordinal);

        var graph = new Graph(cdo, bodies, numeric);
        var found = new List<EndAbilityPath>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var loops = new List<EndAbilityLoop>();
        var truncated = false;
        var steps = 0;

        foreach (var activation in Activations)
        {
            if (graph.Function(activation) is not { } entered) continue;

            var root = Read(entered, entered.ScriptBytecode![0].StatementIndex, graph);

            Follow(root, [], new Dictionary<Segment, int> { [root] = 0 });
        }

        if (truncated) Truncated.Add(cdo.Owner?.Name ?? cdo.Name);

        var timed = found.SelectMany(path => path.Tasks)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<EndAbilityTask>()
            .Where(task => Timed.ContainsKey((task.Task, task.Delegate)))
            .ToArray();

        if (found.Count > 0) Abilities++;

        Waits += timed.Length;
        Unread += timed.Count(task => task.Seconds is null);

        return found;

        // A segment is entered with the count of the tasks before it, so the segment at count n is the handler of task n - 1.
        void Follow(Segment segment, List<EndAbilityTask> tasks, Dictionary<Segment, int> entered)
        {
            if (found.Count >= MaxPaths || ++steps > MaxSteps)
            {
                truncated = true;
                return;
            }

            var returning = loops.Count;

            foreach (var edge in segment.Edges)
            {
                if (!entered.TryGetValue(Read(edge.Body, edge.Entry, graph), out var count) || count == 0) continue;
                if (loops.Skip(returning).Any(loop => loop.To == count - 1)) continue;

                loops.Add(new EndAbilityLoop(tasks.Count - 1, count - 1) { Conditions = edge.Task.Conditions });
            }

            if (segment.Ends && seen.Add($"{Key(tasks)}|{Key(segment.EndConditions)}"))
            {
                found.Add(new EndAbilityPath([.. tasks])
                {
                    EndConditions = segment.EndConditions.Count > 0 ? segment.EndConditions : null,
                    Loops = loops.Count > 0 ? [.. loops] : null,
                });
            }

            foreach (var edge in segment.Edges)
            {
                var next = Read(edge.Body, edge.Entry, graph);

                if (!entered.TryAdd(next, tasks.Count + 1)) continue;

                tasks.Add(edge.Task);
                Follow(next, tasks, entered);
                tasks.RemoveAt(tasks.Count - 1);
                entered.Remove(next);
            }

            loops.RemoveRange(returning, loops.Count - returning);
        }
    }

    private static string Key(IEnumerable<EndAbilityTask> tasks) =>
        string.Join('|', tasks.Select(task =>
            $"{task.Task}.{task.Delegate}:{task.Seconds}:{task.ScaleTimeWithAbilitySpeed}:{string.Join(',', task.Variables?.Keys ?? [])}:{Key(task.Conditions)}"));

    private static string Key(IEnumerable<BranchCondition>? conditions) =>
        string.Join('&', (conditions ?? []).Select(condition => $"{condition.Condition}={condition.Value}"));

    /// <summary>The boolean expression of a branch statement.</summary>
    private static KismetExpression? Tested(KismetExpression branch) => branch switch
    {
        EX_JumpIfNot jump => jump.BooleanExpression,
        EX_PopExecutionFlowIfNot pop => pop.BooleanExpression,
        _ => null,
    };

    /// <summary>A task start under the branches of its handler, after the branches of the handler's caller.</summary>
    private static Edge Under(Edge edge, IReadOnlyList<BranchCondition> conditions) =>
        conditions.Count == 0
            ? edge
            : edge with { Task = edge.Task with { Conditions = [.. conditions.Union(edge.Task.Conditions ?? [])] } };

    /// <summary>
    /// The statements a body runs from an entry offset before it returns: whether one is <c>K2_EndAbility</c>, and the
    /// waits started. A call of the Blueprint's own function or event is read in place. A function entered while it is
    /// being read gives what is read so far, so a cycle of calls ends.
    /// </summary>
    private Segment Read(UStruct body, int entry, Graph graph)
    {
        if (graph.Segments.TryGetValue((body, entry), out var known)) return known;

        var segment = graph.Segments[(body, entry)] = new Segment();
        var paths = graph.PathsOf(body);
        var conditions = graph.ConditionsOf(body);
        var visited = new HashSet<int>();

        // The branches between the segment's entry and a statement. A validity test of the object waited on is left out.
        IReadOnlyList<BranchCondition> Held(KismetExpression statement, string? waited = null) =>
        [
            .. conditions.Between(entry, statement.StatementIndex)
                .SelectMany(side => GraphExpressionText.Conditions(
                    Tested(conditions.Statements[side.At]), side.Held, conditions.Statements, side.At, waited))
                .Distinct(),
        ];

        foreach (var (statement, taken) in paths.From(entry, condition => Commit(condition, paths) is not null))
        {
            if (taken.Any(branch => Commit(branch.Condition, paths) is { } positive && positive != branch.Held)) continue;
            if (!visited.Add(statement.StatementIndex)) continue;

            if (statement is EX_LocalVirtualFunction or EX_LocalFinalFunction or EX_VirtualFunction or EX_FinalFunction &&
                RowSinks.Calling(statement) == EndCall)
            {
                segment.End(Held(statement));
                continue;
            }

            if (statement is EX_AddMulticastDelegate added)
            {
                if (Delegated(added, body, graph) is { } edge)
                {
                    segment.Start(Under(edge, Held(added, DynamicTextFloats.Name((added.Delegate as EX_Context)?.ObjectExpression))));
                }

                continue;
            }

            foreach (var call in CommitTriggers.Invocations(statement, 0))
            {
                if (RowSinks.Calling(call) is not { } called) continue;

                if (call is EX_LocalFinalFunction or EX_LocalVirtualFunction && graph.Function(called) is { } inner)
                {
                    var at = called.StartsWith(Ubergraph, StringComparison.Ordinal)
                        ? CommitTriggers.UbergraphOffset(call)
                        : inner.ScriptBytecode![0].StatementIndex;

                    if (at is { } offset)
                    {
                        var read = Read(inner, offset, graph);
                        var calling = Held(statement);

                        if (read.Ends) segment.End([.. calling.Union(read.EndConditions)]);

                        foreach (var started in read.Edges) segment.Start(Under(started, calling));
                    }

                    continue;
                }

                var parameters = RowSinks.Parameters(call);

                if (parameters.OfType<EX_StructConst>().FirstOrDefault(constant => constant.Struct.Name == LatentInfo) is { } latent &&
                    latent.Properties.OfType<EX_SkipOffsetConst>().FirstOrDefault() is { } linkage)
                {
                    segment.Start(Under(
                        new Edge(Timing(new EndAbilityTask(called), call, body, graph.IndexOf(body, statement), graph), body, (int)linkage.Value, statement.StatementIndex),
                        Held(statement)));

                    continue;
                }

                foreach (var parameter in parameters)
                {
                    if (parameter is not EX_LocalVariable || DynamicTextFloats.Name(parameter) is not { } local) continue;
                    if (Bound(body, graph.IndexOf(body, statement), local, graph) is not { } handler) continue;

                    segment.Start(Under(
                        new Edge(Timing(new EndAbilityTask(called), call, body, graph.IndexOf(body, statement), graph), handler.Body, handler.Entry, statement.StatementIndex),
                        Held(statement)));
                }
            }
        }

        return segment;
    }

    /// <summary>Whether a condition is the result of <c>K2_CommitAbility</c>, and whether it holds when the commit does.</summary>
    private static bool? Commit(KismetExpression? condition, BranchPaths paths)
    {
        var positive = true;
        var value = paths.Value(condition);

        while (value is not null && RowSinks.Invoking(value) is { } inverted && RowSinks.Calling(inverted) == Negation)
        {
            positive = !positive;
            value = paths.Value(RowSinks.Parameters(inverted).FirstOrDefault());
        }

        return value is not null && RowSinks.Invoking(value) is { } call &&
               RowSinks.Calling(call)?.StartsWith(CommitCall, StringComparison.Ordinal) is true
            ? positive
            : null;
    }

    /// <summary>The edge of a delegate added to a task: the task the object local was assigned, and the bound function's entry.</summary>
    private Edge? Delegated(EX_AddMulticastDelegate added, UStruct body, Graph graph)
    {
        if (added.Delegate is not EX_Context { ObjectExpression: { } holder, ContextExpression: { } property }) return null;
        if (DynamicTextFloats.Name(property) is not { } name) return null;
        if (DynamicTextFloats.Name(added.DelegateToAdd) is not { } local) return null;

        var at = graph.IndexOf(body, added);

        if (Bound(body, at, local, graph) is not { } handler) return null;
        if (DynamicTextFloats.Name(holder) is not { } held) return null;

        var statements = body.ScriptBytecode!;

        for (var back = at - 1; back >= 0 && holder is EX_LocalVariable; back--)
        {
            if (statements[back] is not EX_LetObj { Variable: { } variable, Assignment: { } assigned }) continue;
            if (DynamicTextFloats.Name(variable) != held) continue;
            if (RowSinks.Invoking(assigned) is not { } call || RowSinks.Calling(call) is not { } called) break;

            return new Edge(Timing(new EndAbilityTask(called) { Delegate = name }, call, body, back, graph), handler.Body, handler.Entry, added.StatementIndex);
        }

        var declaring = holder is EX_InstanceVariable
            ? null
            : RowSinks.OwnerClass((property as EX_InstanceVariable)?.Variable.New?.ResolvedOwner?.Name);

        return new Edge(new EndAbilityTask(declaring ?? held) { Delegate = name }, handler.Body, handler.Entry, added.StatementIndex);
    }

    /// <summary>The entry of the function bound to a delegate local by the nearest bind before a statement.</summary>
    private static (UStruct Body, int Entry)? Bound(UStruct body, int at, string local, Graph graph)
    {
        var statements = body.ScriptBytecode!;

        for (var back = at - 1; back >= 0; back--)
        {
            if (statements[back] is not EX_BindDelegate bound || DynamicTextFloats.Name(bound.Delegate) != local) continue;
            if (graph.Function(bound.FunctionName.Text) is not { } handler) return null;

            foreach (var statement in handler.ScriptBytecode!)
            {
                if (CommitTriggers.UbergraphOffset(statement) is not { } offset) continue;
                if (RowSinks.Invoking(statement) is not { } call || RowSinks.Calling(call) is not { } called) continue;

                return graph.Function(called) is { } ubergraph ? (ubergraph, offset) : null;
            }

            return (handler, handler.ScriptBytecode[0].StatementIndex);
        }

        return null;
    }

    private EndAbilityTask Timing(EndAbilityTask task, KismetExpression call, UStruct body, int at, Graph graph)
    {
        if (!Timed.TryGetValue((task.Task, task.Delegate), out var timed)) return task;

        var parameters = RowSinks.Parameters(call);
        var variables = new SortedDictionary<string, double>(StringComparer.Ordinal);
        var seconds = Number(parameters.ElementAtOrDefault(timed), body, at, graph, variables, 0);

        if (task.Task == Timer)
        {
            var delay = Number(parameters.ElementAtOrDefault(StartDelayParameter), body, at, graph, variables, 0);
            var variance = Number(parameters.ElementAtOrDefault(StartDelayVarianceParameter), body, at, graph, variables, 0);

            seconds = variance is 0 ? seconds + delay : null;
        }

        return task with
        {
            Seconds = PropertyWalk.AsNumber((float?)seconds),
            Variables = variables.Count > 0 ? variables : null,
            ScaleTimeWithAbilitySpeed = task.Task == ScaledWait ? parameters.ElementAtOrDefault(ScaleParameter) is EX_True : null,
        };
    }

    /// <summary>
    /// The value a local holds at a statement, through casts: the local's nearest assignment before the statement, and
    /// that assignment's index. A local with no assignment before the statement is returned as it is.
    /// </summary>
    private static (KismetExpression? Value, int At) Assigned(KismetExpression? expression, UStruct body, int at)
    {
        var statements = body.ScriptBytecode!;

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (expression is EX_Cast cast)
            {
                expression = cast.Target;
                continue;
            }

            if (expression is not (EX_LocalVariable or EX_LocalOutVariable) || DynamicTextFloats.Name(expression) is not { } local) break;

            var found = -1;

            for (var back = at - 1; back >= 0 && found < 0; back--)
            {
                var (variable, value) = statements[back] switch
                {
                    EX_Let { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                    EX_LetObj { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                    EX_LetBool { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                    _ => (null, null),
                };

                if (variable is not (EX_LocalVariable or EX_LocalOutVariable) || DynamicTextFloats.Name(variable) != local) continue;

                expression = value;
                found = back;
            }

            if (found < 0) break;

            at = found;
        }

        return (expression, at);
    }

    /// <summary>
    /// The number an expression at a statement comes to from literals, class defaults and constants rows, with each
    /// variable read. A variable a graph assigns and a value of the run give none.
    /// </summary>
    private double? Number(
        KismetExpression? expression, UStruct body, int at, Graph graph, IDictionary<string, double> variables, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        var (value, from) = Assigned(expression, body, at);

        switch (value)
        {
            case EX_FloatConst constant: return constant.Value;
            case EX_DoubleConst constant: return constant.Value;
            case EX_IntConst constant: return constant.Value;
            case EX_ByteConst constant: return constant.Value;
            case EX_IntOne: return 1;
            case EX_IntZero: return 0;

            case EX_CallMath or EX_FinalFunction when RowSinks.Calling(value) is { } called:
            {
                var parameters = RowSinks.Parameters(value);

                double? Argument(int index) => Number(parameters.ElementAtOrDefault(index), body, from, graph, variables, depth + 1);

                if (called.StartsWith(ScalableRead, StringComparison.Ordinal))
                {
                    var source = Assigned(parameters.FirstOrDefault(), body, from).Value;

                    if (Held(source, graph) is not { } scalable) return null;
                    if (MagnitudeCalculations.Scalable(scalable, constants) is not { } resolved) return null;

                    variables[DynamicTextFloats.Name(source)!] = resolved;

                    return resolved;
                }

                if (called.StartsWith(Conversion, StringComparison.Ordinal)) return Argument(0);

                var left = Argument(0);
                var right = Argument(1);

                return called.Split('_')[0] switch
                {
                    "Multiply" => left * right,
                    "Add" => left + right,
                    "Subtract" => left - right,
                    "Divide" => right is 0 ? null : left / right,
                    "FMax" or "Max" => left is { } first && right is { } second ? Math.Max(first, second) : null,
                    "FMin" or "Min" => left is { } first && right is { } second ? Math.Min(first, second) : null,
                    _ => null,
                };
            }

            case EX_InstanceVariable when DynamicTextFloats.Name(value) is { } name && !graph.Assigned.Contains(name):
            {
                var held = ClassDefaults.Inherited(graph.Cdo, name, provider);
                var declared = held is null && graph.Numeric.Contains(name) ? 0 : PropertyWalk.AsNumber(held?.GenericValue);

                if (declared is not { } number) return null;

                variables[name] = number;

                return number;
            }

            case EX_StructMemberContext member when DynamicTextFloats.Name(member) is { } field:
            {
                var source = Assigned(member.StructExpression, body, from).Value;

                if (Held(source, graph) is not { } holder) return null;

                var read = PropertyWalk.Get(holder, field)?.GenericValue;
                var number = read is FScriptStruct { StructType: IPropertyHolder scalable }
                    ? MagnitudeCalculations.Scalable(scalable, constants)
                    : PropertyWalk.AsNumber(read);

                if (number is { } found) variables[$"{DynamicTextFloats.Name(source)}.{field}"] = found;

                return number;
            }
        }

        return null;
    }

    /// <summary>The struct an instance variable holds on the class default object.</summary>
    private IPropertyHolder? Held(KismetExpression? expression, Graph graph)
    {
        if (expression is not EX_InstanceVariable || DynamicTextFloats.Name(expression) is not { } name) return null;
        if (graph.Assigned.Contains(name)) return null;

        return ClassDefaults.Inherited(graph.Cdo, name, provider)?.GenericValue is FScriptStruct { StructType: IPropertyHolder holder }
            ? holder
            : null;
    }
}
