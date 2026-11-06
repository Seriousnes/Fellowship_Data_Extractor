using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Reads the proc-per-minute rolls an ability's Blueprint makes on <c>CRRandomStreamComponent.RollProcPerMinute</c>.
/// The call takes the stream tag, the rate and whether the component multiplies the rate by the hero's Haste. The
/// rate is the Blueprint's own arithmetic over a tuning row and the attributes it reads: Dragonbone Dice passes
/// <c>ArmorSetBonus.PowerBonusOnCrit.PPM × (1 + CritChance)</c> with Haste off, and Phantom Volley passes
/// <c>Ink.BouncyProjectile.FreeCastFromAoeAttack.PPM</c> with Haste on.
/// </summary>
public sealed class ProcRolls(ConstantsTable constants, AbstractFileProvider provider)
{
    private const string Roll = "RollProcPerMinute";
    private const string ScalableRead = "GetScalableFloat";
    private const string AttributeRead = "GetFloatAttribute";
    private const string Conversion = "Conv_";
    private const string CurveField = "Curve";
    private const string RowField = "RowName";
    private const string ValueField = "Value";
    private const string NoRow = "None";
    private const string Times = " × ";
    private const string ClassTest = "ClassIsChildOf";
    private const string AbilityTagTest = "HasTag";
    private const string CooldownTest = "Greater_DoubleDouble";
    private const string CooldownRead = "CrGetCooldownTimeRemaining";
    private const string Cooldown = "Cooldown";
    private const int RateParameter = 1;
    private const int HasteParameter = 2;
    private const int MaxDepth = 24;

    public int Calls { get; private set; }

    public List<string> Unread { get; } = [];

    /// <summary>A rendered term of the rate: its text, whether it is a sum, the row it reads and the attributes it names.</summary>
    private sealed record Term(string Text, bool Sum, string? Row, double? Value, IReadOnlyList<string> Attributes)
    {
        public bool Constant => Row is null && Attributes.Count == 0;

        public string Factor => Sum ? $"({Text})" : Text;
    }

    /// <summary>The exits a Blueprint takes before a roll.</summary>
    private sealed record RollExits(IReadOnlyList<string> Classes, IReadOnlyList<string> Tags, bool OnCooldown);

    private static readonly RollExits NoExits = new([], [], false);

    /// <summary>
    /// The tests every path to the roll passes on the side that leaves the ability unmatched: <c>ClassIsChildOf</c> and
    /// <c>HasTag</c> on the committed ability, and <c>CrGetCooldownTimeRemaining</c> above 0.
    /// </summary>
    private RollExits Exits(UStruct body, KismetExpression statement, IReadOnlyList<int> entries)
    {
        var paths = new BranchPaths(body);
        var reached = entries
            .SelectMany(entry => paths.From(entry, condition => Gate(paths.Value(condition), paths)))
            .Where(step => step.Statement.StatementIndex == statement.StatementIndex)
            .Select(step => step.Taken.Where(branch => !branch.Held).Select(branch => paths.Value(branch.Condition)).ToArray())
            .ToArray();

        if (paths.Truncated) Unread.Add($"{body.Owner?.Name}:{body.Name} has more paths to a roll than the walk follows");

        if (reached.Length == 0) return NoExits;

        string[] All(Func<KismetExpression?, string?> reading) =>
            [
                .. reached
                    .Select(conditions => conditions.Select(reading).OfType<string>())
                    .Aggregate((held, next) => held.Intersect(next, StringComparer.Ordinal))
                    .Distinct(StringComparer.Ordinal),
            ];

        return new RollExits(All(ExcludedClass), All(ExcludedTag), All(condition => OnCooldown(condition, paths) ? Cooldown : null).Length > 0);
    }

    private static bool Gate(KismetExpression? condition, BranchPaths paths) =>
        ExcludedClass(condition) is not null || ExcludedTag(condition) is not null || OnCooldown(condition, paths);

    private static string? ExcludedClass(KismetExpression? condition) =>
        Called(condition, ClassTest) is { } call &&
        RowSinks.Parameters(call).ElementAtOrDefault(1) is EX_ObjectConst { Value: { } type } &&
        PropertyWalk.AsString(type) is { } path
            ? path[(path.LastIndexOfAny(['.', '/']) + 1)..]
            : null;

    private static string? ExcludedTag(KismetExpression? condition) =>
        Called(condition, AbilityTagTest) is { } call &&
        RowSinks.Parameters(call).ElementAtOrDefault(1) is EX_StructConst tag
            ? tag.Properties.OfType<EX_NameConst>().FirstOrDefault()?.Value.Text
            : null;

    private static bool OnCooldown(KismetExpression? condition, BranchPaths paths) =>
        Called(condition, CooldownTest) is { } call &&
        Called(paths.Value(RowSinks.Parameters(call).FirstOrDefault()), CooldownRead) is not null &&
        RowSinks.Parameters(call).ElementAtOrDefault(1) is EX_DoubleConst { Value: 0 };

    private static KismetExpression? Called(KismetExpression? expression, string function) =>
        RowSinks.Invoking(expression) is { } call && RowSinks.Calling(call) == function ? call : null;

    private static int[] Entries(IEnumerable<UStruct> bodies) =>
        [.. bodies.SelectMany(body => body.ScriptBytecode!).Select(CommitTriggers.UbergraphOffset).OfType<int>().Distinct()];

    public IReadOnlyList<ProcRoll> Of(UObject? cdo, double? cooldown = null)
    {
        if (cdo?.Owner is not { } package) return [];

        var bodies = package.GetExports()
            .OfType<UStruct>()
            .Where(export => export.ScriptBytecode is { Length: > 0 })
            .ToArray();

        var entries = Entries(bodies);
        var found = new List<ProcRoll>();

        foreach (var body in bodies)
        {
            var locals = Locals(body);
            var gating = TagGatedReads.Gating(body, cdo);

            foreach (var statement in body.ScriptBytecode!)
            {
                var gates = gating.GetValueOrDefault(statement.StatementIndex);

                foreach (var call in Invocations(statement, 0))
                {
                    if (RowSinks.Calling(call) != Roll) continue;

                    Calls++;
                    var parameters = RowSinks.Parameters(call);
                    var where = $"{package.Name}:{body.Name}";

                    if (parameters.ElementAtOrDefault(HasteParameter) is not { } hasteFlag || hasteFlag is not (EX_True or EX_False))
                    {
                        Unread.Add($"{where} passes ScaleWithHaste as other than a literal");
                        continue;
                    }

                    if (Rendered(cdo, locals, parameters.ElementAtOrDefault(RateParameter), 0) is not { Row: { } row } term)
                    {
                        Unread.Add($"{where} passes a rate the reader cannot write out as a row and attributes");
                        continue;
                    }

                    var hasted = hasteFlag is EX_True;
                    var exits = Exits(body, statement, entries);

                    var roll = new ProcRoll(row)
                    {
                        ExcludedAbilityClasses = exits.Classes.Count > 0 ? exits.Classes : null,
                        ExcludedAbilityTags = exits.Tags.Count > 0 ? exits.Tags : null,
                        InternalCooldown = exits.OnCooldown ? cooldown : null,
                        ProcsPerMinute = term.Value,
                        Formula = hasted ? $"{term.Factor}{Times}(1 + {ProcRoll.HasteAttribute})" : term.Text,
                        ScalesOff = hasted ? [.. term.Attributes, ProcRoll.HasteAttribute] : term.Attributes,
                        ScalesWithHaste = hasted,
                        Gates = gates is { Count: > 0 } ? [.. gates] : null,
                    };

                    if (!found.Any(other => Same(other, roll))) found.Add(roll);
                }
            }
        }

        return found;
    }

    public static bool Same(ProcRoll first, ProcRoll second) =>
        first.Row == second.Row && first.Formula == second.Formula && first.ScalesWithHaste == second.ScalesWithHaste &&
        (first.Gates ?? []).SequenceEqual(second.Gates ?? [], StringComparer.Ordinal);

    private Term? Rendered(
        UObject cdo, IReadOnlyDictionary<string, List<KismetExpression>> locals, KismetExpression? expression, int depth)
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
            case EX_Cast cast: return Rendered(cdo, locals, cast.Target, depth + 1);
            case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                return Agreed(locals.GetValueOrDefault(local)?.Select(assigned => Rendered(cdo, locals, assigned, depth + 1)));
            case EX_InstanceVariable when DynamicTextFloats.Name(expression) is { } member:
                return PropertyWalk.AsNumber(ClassDefaults.Inherited(cdo, member, provider)?.GenericValue) is { } declared
                    ? Number(declared)
                    : null;
        }

        if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

        var parameters = RowSinks.Parameters(call);

        if (called.StartsWith(ScalableRead, StringComparison.Ordinal)) return Read(cdo, parameters.FirstOrDefault());
        if (called.Equals(AttributeRead, StringComparison.Ordinal)) return Attribute(parameters);

        if (called.StartsWith(Conversion, StringComparison.Ordinal))
        {
            return Rendered(cdo, locals, parameters.FirstOrDefault(), depth + 1);
        }

        if (parameters.Count != 2) return null;

        var left = Rendered(cdo, locals, parameters[0], depth + 1);
        var right = Rendered(cdo, locals, parameters[1], depth + 1);
        if (left is null || right is null) return null;

        if (called.StartsWith("Add_", StringComparison.Ordinal))
        {
            var (first, second) = right.Constant && !left.Constant ? (right, left) : (left, right);
            return Joined(first, second, $"{first.Text} + {second.Text}", sum: true);
        }

        if (called.StartsWith("Subtract_", StringComparison.Ordinal))
        {
            return Joined(left, right, $"{left.Text} − {right.Factor}", sum: true);
        }

        if (called.StartsWith("Multiply_", StringComparison.Ordinal))
        {
            var (first, second) = right.Row is not null && left.Row is null ? (right, left) : (left, right);
            return Joined(first, second, $"{first.Factor}{Times}{second.Factor}", sum: false);
        }

        if (called.StartsWith("Divide_", StringComparison.Ordinal))
        {
            return Joined(left, right, $"{left.Factor} ÷ {right.Factor}", sum: false);
        }

        return null;
    }

    /// <summary>The one term every assignment of a local writes out as, so a local assigned the same read twice reads once.</summary>
    private static Term? Agreed(IEnumerable<Term?>? rendered)
    {
        Term? agreed = null;

        foreach (var term in rendered ?? [])
        {
            if (term is null) return null;
            if (agreed is not null && (agreed.Text != term.Text || agreed.Row != term.Row)) return null;
            agreed = term;
        }

        return agreed;
    }

    private static Term? Joined(Term first, Term second, string text, bool sum)
    {
        if (first.Row is not null && second.Row is not null) return null;

        return new Term(
            text,
            sum,
            first.Row ?? second.Row,
            first.Value ?? second.Value,
            [.. first.Attributes.Concat(second.Attributes).Distinct(StringComparer.Ordinal)]);
    }

    private static Term Number(double value) =>
        new(value.ToString("0.####", CultureInfo.InvariantCulture), false, null, null, []);

    private Term? Read(UObject cdo, KismetExpression? expression)
    {
        if (Property(expression) is not { } property) return null;
        if (Held(ClassDefaults.Inherited(cdo, property, provider)?.GenericValue) is not { } scalable) return null;

        var row = PropertyWalk.GetStruct(scalable, CurveField) is { } curve ? PropertyWalk.GetString(curve, RowField) : null;
        if (row is null || row.Length == 0 || row.Equals(NoRow, StringComparison.Ordinal)) return null;

        var multiplier = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ValueField)?.GenericValue) ?? 1;
        var value = constants.Lookup(row).Record?.Value is { } held ? multiplier * held : (double?)null;

        return new Term(ProcRoll.Rate, false, row, value, []);
    }

    private static IPropertyHolder? Held(object? value) => value switch
    {
        FScriptStruct { StructType: IPropertyHolder inner } => inner,
        IPropertyHolder direct => direct,
        _ => null,
    };

    private static string? Property(KismetExpression? expression) => expression switch
    {
        EX_InstanceVariable variable when variable.Variable.New?.Path is { Length: > 0 } path => path[^1].Text,
        EX_Context context => Property(context.ContextExpression),
        EX_Cast cast => Property(cast.Target),
        _ => null,
    };

    private static Term? Attribute(IReadOnlyList<KismetExpression> parameters)
    {
        var attribute = parameters
            .OfType<EX_StructConst>()
            .SelectMany(constant => constant.Properties.OfType<EX_StringConst>())
            .Select(text => text.Value)
            .FirstOrDefault(text => text.Length > 0 && !text.Contains('.') && !text.Contains('/'));

        return attribute is null ? null : new Term(attribute, false, null, null, [attribute]);
    }

    private static Dictionary<string, List<KismetExpression>> Locals(UStruct body)
    {
        var held = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

        foreach (var statement in body.ScriptBytecode ?? [])
        {
            var (variable, value) = statement switch
            {
                EX_Let { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                EX_LetObj { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                EX_LetBool { Variable: { } assigned, Assignment: { } to } => (assigned, to),
                _ => (null, null),
            };

            if (variable is null || variable is EX_InstanceVariable) continue;
            if (DynamicTextFloats.Name(variable) is not { } local) continue;

            if (!held.TryGetValue(local, out var values)) held[local] = values = [];
            values.Add(value!);
        }

        return held;
    }

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
