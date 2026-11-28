using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Reads the magnitude an ability's Blueprint assigns to a spec of an effect running
/// <c>CRModifyStaggerExecution</c>, under that execution's set-by-caller tag. The execution passes the magnitude to
/// <c>AuthUpdateStaggerAndLog</c> unscaled (binary 26320967, 0x1451091E0), so the expression is the whole delta.
/// A variable is read at its nearest write before the assignment. An expression with a term the reader has no
/// node for gives no delta and a line in <see cref="Unread"/>.
/// </summary>
public sealed class StaggerDeltas(
    AbstractFileProvider provider,
    ConstantsTable constants,
    IReadOnlyList<TagRecord> tags,
    Func<string, UObject?> effectDefault)
{
    public const string Execution = "CRModifyStaggerExecution";

    private const string Assignment = "AssignTagSetByCallerMagnitude";
    private const string ScalableRead = "GetScalableFloat";
    private const string AttributeRead = "GetFloatAttribute";
    private const string ArrayRead = "Array_Get";
    private const string Selecting = "SelectFloat";
    private const string Random = "RandomFloatInRange";
    private const string Conversion = "Conv_";
    private const string Getter = "Get";
    private const string Making = "Make";
    private const string SpecMaking = "EffectSpec";
    private const string OwnerToken = "Owning";
    private const string TargetToken = "TargetData";
    private const string EffectTable = "GameplayEffect";
    private const string ClassSuffix = "_C";
    private const string Source = "Source";
    private const string Target = "Target";
    private const string Multiply = "Multiply";
    private const string Add = "Add";
    private const string Subtract = "Subtract";
    private const string Divide = "Divide";
    private const string Min = "Min";
    private const string Max = "Max";
    private const int MagnitudeParameter = 2;
    private const int MaxDepth = 40;

    private static readonly (string Prefix, string Op)[] Arithmetic =
    [
        ("Multiply_", Multiply), ("Add_", Add), ("Subtract_", Subtract), ("Divide_", Divide),
        ("FMin", Min), ("FMax", Max), ("Min", Min), ("Max", Max),
    ];

    private readonly Dictionary<string, TagRecord> _effects = tags
        .Where(tag => tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
        .GroupBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IReadOnlySet<string>> _dataTags = new(StringComparer.OrdinalIgnoreCase);

    public int Calls { get; private set; }

    public int Read { get; private set; }

    public List<string> Unread { get; } = [];

    private sealed record Write(UStruct Body, int At, string Name, bool Member, KismetExpression Value);

    private sealed record Pass(UStruct Body, int At, string Name, KismetExpression Call);

    private sealed class Graph(UObject cdo, IReadOnlyList<Write> writes, IReadOnlyList<Pass> passes)
    {
        public UObject Cdo { get; } = cdo;

        /// <summary>The write of a variable nearest before a statement, or the first after it where none precedes.</summary>
        public Write? Written(UStruct body, int at, string name, bool member)
        {
            var own = writes.Where(write => write.Member == member && ReferenceEquals(write.Body, body) && write.Name == name).ToArray();

            if (own.Where(write => write.At < at).MaxBy(write => write.At) is { } before) return before;
            if (own.MinBy(write => write.At) is { } after) return after;
            if (!member) return null;

            var others = writes.Where(write => write.Member && write.Name == name).ToArray();

            return others.Length == 1 ? others[0] : null;
        }

        public IEnumerable<Pass> Passing(UStruct body, string name) =>
            passes.Where(pass => ReferenceEquals(pass.Body, body) && pass.Name == name);
    }

    public IReadOnlyList<StaggerDeltaSource>? Of(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return null;

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(body => body.ScriptBytecode is { Length: > 0 })
            .ToArray();

        Graph? graph = null;
        var found = new List<StaggerDeltaSource>();

        foreach (var body in bodies)
        {
            var bytecode = body.ScriptBytecode!;

            for (var at = 0; at < bytecode.Length; at++)
            {
                foreach (var call in Invocations(bytecode[at], 0))
                {
                    if (RowSinks.Calling(call) != Assignment) continue;

                    var parameters = RowSinks.Parameters(call);
                    if (SetByCallerDurations.Tag(parameters.ElementAtOrDefault(1), cdo) is not { } tag) continue;

                    graph ??= Graphed(cdo, bodies);

                    if (Effect(graph, body, at, parameters.FirstOrDefault(), 0) is not { } effect) continue;
                    if (!DataTags(effect).Contains(tag)) continue;

                    Calls++;

                    if (Evaluate(graph, body, at, parameters.ElementAtOrDefault(MagnitudeParameter), 0) is not { } term)
                    {
                        Unread.Add($"{package.Name}.{body.Name}: the {tag} magnitude of {effect} has a term with no node");
                        continue;
                    }

                    Read++;

                    var record = _effects[effect];
                    var delta = new StaggerDeltaSource(
                        new IconSource(record.Table, record.NetId, record.Name), tag, Rendered(term, false), term);

                    if (!found.Any(held => held.Effect == delta.Effect && held.Formula == delta.Formula)) found.Add(delta);
                }
            }
        }

        return found.Count == 0 ? null : found;
    }

    private static Graph Graphed(UObject cdo, IReadOnlyList<UStruct> bodies)
    {
        var writes = new List<Write>();
        var passes = new List<Pass>();

        foreach (var body in bodies)
        {
            var bytecode = body.ScriptBytecode!;

            for (var at = 0; at < bytecode.Length; at++)
            {
                if (Assigned(bytecode[at]) is var (variable, value) && DynamicTextFloats.Name(variable) is { } name)
                {
                    writes.Add(new Write(body, at, name, variable is EX_InstanceVariable, value));
                }

                foreach (var call in Invocations(bytecode[at], 0))
                {
                    foreach (var parameter in RowSinks.Parameters(call))
                    {
                        if (parameter is EX_LocalVariable or EX_LocalOutVariable && DynamicTextFloats.Name(parameter) is { } passed)
                        {
                            passes.Add(new Pass(body, at, passed, call));
                        }
                    }
                }
            }
        }

        return new Graph(cdo, writes, passes);
    }

    private static (KismetExpression Variable, KismetExpression Value)? Assigned(KismetExpression statement) => statement switch
    {
        EX_Let { Variable: { } variable, Assignment: { } value } => (variable, value),
        EX_LetBase { Variable: { } variable, Assignment: { } value } => (variable, value),
        _ => null,
    };

    /// <summary>The set-by-caller tags the effect's <see cref="Execution"/> modifiers read.</summary>
    private IReadOnlySet<string> DataTags(string effect)
    {
        if (_dataTags.TryGetValue(effect, out var known)) return known;

        var read = effectDefault(effect) is { } loaded
            ? CalculationModifiers.SetByCallers(loaded, provider)
                .Where(modifier => modifier.Execution.Equals(Execution, StringComparison.Ordinal))
                .Select(modifier => modifier.DataTag)
                .ToHashSet(StringComparer.Ordinal)
            : [];

        return _dataTags[effect] = read;
    }

    /// <summary>The effect class of the spec an expression holds, followed to the spec maker's class argument.</summary>
    private string? Effect(Graph graph, UStruct body, int at, KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_ObjectConst constant:
                return Known(PropertyWalk.AsString(constant.Value));
            case EX_Cast cast:
                return Effect(graph, body, at, cast.Target, depth + 1);
            case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                if (Known(PropertyWalk.AsString(ClassDefaults.Inherited(graph.Cdo, member, provider)?.GenericValue)) is { } declared)
                {
                    return declared;
                }

                return graph.Written(body, at, member, true) is { } set ? Effect(graph, set.Body, set.At, set.Value, depth + 1) : null;
            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                return graph.Written(body, at, local, false) is { } written
                    ? Effect(graph, written.Body, written.At, written.Value, depth + 1)
                    : null;
        }

        if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

        var parameters = RowSinks.Parameters(call);

        if (called.Contains(Making, StringComparison.Ordinal) && called.Contains(SpecMaking, StringComparison.Ordinal))
        {
            return parameters
                .Select(parameter => Effect(graph, body, at, parameter, depth + 1))
                .FirstOrDefault(effect => effect is not null);
        }

        return Effect(graph, body, at, parameters.FirstOrDefault(), depth + 1);
    }

    private string? Known(string? path)
    {
        if (path is null) return null;

        var leaf = path[(path.LastIndexOfAny(['.', '/']) + 1)..];
        if (leaf.EndsWith(ClassSuffix, StringComparison.Ordinal)) leaf = leaf[..^ClassSuffix.Length];

        return _effects.ContainsKey(leaf) ? leaf : null;
    }

    private MagnitudeTerm? Evaluate(Graph graph, UStruct body, int at, KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_IntConst value: return Number(value.Value);
            case EX_Int64Const value: return Number(value.Value);
            case EX_ByteConst value: return Number(value.Value);
            case EX_IntConstByte value: return Number(value.Value);
            case EX_FloatConst value: return Number(value.Value);
            case EX_DoubleConst value: return Number(value.Value);
            case EX_IntOne: return Number(1);
            case EX_IntZero: return Number(0);
            case EX_Cast cast: return Evaluate(graph, body, at, cast.Target, depth + 1);

            case EX_StructMemberContext member when DynamicTextFloats.Name(member) is { } field:
                return Event(graph, body, at, member.StructExpression, depth + 1) is { } raised
                    ? new MagnitudeTerm { Read = field, Of = raised }
                    : null;

            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                return graph.Written(body, at, local, false) is { } written
                    ? Evaluate(graph, written.Body, written.At, written.Value, depth + 1)
                    : null;

            case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                if (graph.Written(body, at, member, true) is { } set)
                {
                    return Evaluate(graph, set.Body, set.At, set.Value, depth + 1);
                }

                return PropertyWalk.AsNumber(ClassDefaults.Inherited(graph.Cdo, member, provider)?.GenericValue) is { } declared
                    ? Number(declared)
                    : null;
        }

        if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

        var parameters = RowSinks.Parameters(call);

        if (expression is EX_Context context && parameters.Count == 0 && called.StartsWith(Getter, StringComparison.Ordinal))
        {
            return Whose(graph, body, at, context.ObjectExpression, [], depth + 1) is { } holder
                ? new MagnitudeTerm { Read = called, Of = holder }
                : null;
        }

        if (called.StartsWith(ScalableRead, StringComparison.Ordinal)) return Scalable(graph, parameters.FirstOrDefault());

        if (called.Equals(AttributeRead, StringComparison.Ordinal))
        {
            return parameters.Select(Attribute).FirstOrDefault(name => name is not null) is { } attribute &&
                   Whose(graph, body, at, parameters.FirstOrDefault(), [], depth + 1) is { } holder
                ? new MagnitudeTerm { Attribute = attribute, Of = holder }
                : null;
        }

        if (called.StartsWith(Conversion, StringComparison.Ordinal))
        {
            return Evaluate(graph, body, at, parameters.FirstOrDefault(), depth + 1);
        }

        if (called.Equals(Selecting, StringComparison.Ordinal)) return Selected(graph, body, at, parameters, depth);

        var op = called.Equals(Random, StringComparison.Ordinal)
            ? Random
            : Arithmetic.FirstOrDefault(entry => called.StartsWith(entry.Prefix, StringComparison.Ordinal)).Op;

        if (op is null || parameters.Count != 2) return null;

        var left = Evaluate(graph, body, at, parameters[0], depth + 1);
        var right = Evaluate(graph, body, at, parameters[1], depth + 1);

        return left is null || right is null ? null : new MagnitudeTerm { Op = op, Operands = [left, right] };
    }

    /// <summary>
    /// <c>SelectFloat(A, B, bPickA)</c> with the condition a comparison of its own two values: the lesser or the
    /// greater of them.
    /// </summary>
    private MagnitudeTerm? Selected(
        Graph graph, UStruct body, int at, IReadOnlyList<KismetExpression> parameters, int depth)
    {
        if (parameters.Count != 3) return null;

        var first = Evaluate(graph, body, at, parameters[0], depth + 1);
        var second = Evaluate(graph, body, at, parameters[1], depth + 1);
        if (first is null || second is null) return null;

        var condition = parameters[2];
        var (conditionBody, conditionAt) = (body, at);

        for (var hop = 0; hop < MaxDepth && condition is EX_LocalVariable or EX_LocalOutVariable; hop++)
        {
            if (DynamicTextFloats.Name(condition) is not { } local) return null;
            if (graph.Written(conditionBody, conditionAt, local, false) is not { } written) return null;

            (conditionBody, conditionAt, condition) = (written.Body, written.At, written.Value);
        }

        if (RowSinks.Invoking(condition) is not { } comparison || RowSinks.Calling(comparison) is not { } compared) return null;

        var compares = RowSinks.Parameters(comparison);
        if (compares.Count != 2) return null;

        var lesser = compared.StartsWith("Less", StringComparison.Ordinal);
        if (!lesser && !compared.StartsWith("Greater", StringComparison.Ordinal)) return null;

        var left = Evaluate(graph, conditionBody, conditionAt, compares[0], depth + 1);
        var right = Evaluate(graph, conditionBody, conditionAt, compares[1], depth + 1);
        if (left is null || right is null) return null;

        var (picked, other) = (Rendered(first, false), Rendered(second, false));
        var (low, high) = (Rendered(left, false), Rendered(right, false));

        if (low == picked && high == other) return new MagnitudeTerm { Op = lesser ? Min : Max, Operands = [first, second] };
        if (low == other && high == picked) return new MagnitudeTerm { Op = lesser ? Max : Min, Operands = [first, second] };

        return null;
    }

    private MagnitudeTerm? Scalable(Graph graph, KismetExpression? reading)
    {
        if (reading is not EX_InstanceVariable || DynamicTextFloats.Name(reading) is not { } property) return null;

        if (ClassDefaults.Inherited(graph.Cdo, property, provider)?.GenericValue is not
            FScriptStruct { StructType: IPropertyHolder scalable })
        {
            return null;
        }

        var (value, row) = MagnitudeCalculations.Read(scalable, constants);
        if (value is null) return null;

        return row is null ? Number(value.Value) : new MagnitudeTerm { Row = row, Value = Rounded(value.Value) };
    }

    /// <summary>
    /// The character an expression holds: <c>Source</c> for the ability's owner, <c>Target</c> for one of the target
    /// data. A local with no write is an out parameter, read through the call it is passed to.
    /// </summary>
    private static string? Whose(
        Graph graph, UStruct body, int at, KismetExpression? expression, HashSet<string> visited, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;

        switch (expression)
        {
            case EX_Cast cast:
                return Whose(graph, body, at, cast.Target, visited, depth + 1);
            case EX_CastBase cast:
                return Whose(graph, body, at, cast.Target, visited, depth + 1);
            case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                return graph.Written(body, at, member, true) is { } set
                    ? Whose(graph, set.Body, set.At, set.Value, visited, depth + 1)
                    : null;
            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                if (graph.Written(body, at, local, false) is { } written)
                {
                    return Whose(graph, written.Body, written.At, written.Value, visited, depth + 1);
                }

                if (!visited.Add(local)) return null;

                return graph.Passing(body, local)
                    .Select(pass => Passed(graph, pass, local, visited, depth + 1))
                    .FirstOrDefault(holder => holder is not null);
        }

        if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

        return Named(called);
    }

    private static string? Passed(Graph graph, Pass pass, string local, HashSet<string> visited, int depth)
    {
        if (RowSinks.Calling(pass.Call) is not { } called) return null;
        if (Named(called) is { } holder) return holder;
        if (!called.Equals(ArrayRead, StringComparison.Ordinal)) return null;

        var array = RowSinks.Parameters(pass.Call).FirstOrDefault();

        return DynamicTextFloats.Name(array) == local ? null : Whose(graph, pass.Body, pass.At, array, visited, depth + 1);
    }

    private static string? Named(string called) =>
        called.Contains(OwnerToken, StringComparison.Ordinal) ? Source
        : called.Contains(TargetToken, StringComparison.Ordinal) ? Target
        : null;

    /// <summary>The event a struct local is a copy of: the event parameter's name after its node prefix.</summary>
    private static string? Event(Graph graph, UStruct body, int at, KismetExpression? expression, int depth)
    {
        if (expression is null || depth > MaxDepth) return null;
        if (expression is not (EX_LocalVariable or EX_LocalOutVariable)) return null;
        if (DynamicTextFloats.Name(expression) is not { } local) return null;

        return graph.Written(body, at, local, false) is { } written
            ? Event(graph, written.Body, written.At, written.Value, depth + 1)
            : local[(local.LastIndexOf('_') + 1)..];
    }

    private static string? Attribute(KismetExpression? expression) => expression is EX_StructConst constant
        ? constant.Properties.OfType<EX_StringConst>()
            .Select(text => text.Value)
            .FirstOrDefault(text => text.Length > 0 && !text.Contains('.') && !text.Contains('/'))
        : null;

    private static MagnitudeTerm Number(double value) => new() { Constant = Rounded(value) };

    private static double Rounded(double value) => Math.Round(value, 6);

    private static string Rendered(MagnitudeTerm term, bool nested)
    {
        if (term.Constant is { } constant) return constant.ToString(CultureInfo.InvariantCulture);
        if (term.Row is { } row) return row[(row.LastIndexOf('.') + 1)..];
        if (term.Attribute is { } attribute) return $"{term.Of}.{attribute}";
        if (term.Read is { } read) return $"{term.Of}.{read}";

        var operands = term.Operands ?? [];

        switch (term.Op)
        {
            case Multiply:
                return string.Join(" × ", Flat(term, Multiply).Select(operand => Rendered(operand, true)));
            case Add:
                return Grouped(string.Join(" + ", Flat(term, Add).Select(operand => Rendered(operand, true))), nested);
            case Subtract:
                return Grouped($"{Rendered(operands[0], true)} - {Rendered(operands[1], true)}", nested);
            case Divide:
                return Grouped($"{Rendered(operands[0], true)} / {Rendered(operands[1], true)}", nested);
            case Min or Max:
                return $"{term.Op.ToLowerInvariant()}({string.Join(", ", operands.Select(operand => Rendered(operand, false)))})";
            default:
                return $"{term.Op}({string.Join(", ", operands.Select(operand => Rendered(operand, false)))})";
        }
    }

    private static string Grouped(string text, bool nested) => nested ? $"({text})" : text;

    private static IEnumerable<MagnitudeTerm> Flat(MagnitudeTerm term, string op) =>
        term.Op == op && term.Operands is { } operands ? operands.SelectMany(operand => Flat(operand, op)) : [term];

    private static IEnumerable<KismetExpression> Invocations(KismetExpression? expression, int depth)
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
