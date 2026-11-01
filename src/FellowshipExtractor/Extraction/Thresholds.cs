using System.Globalization;

using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Kismet;
using CUE4Parse.UE4.Objects.GameplayTags;
using CUE4Parse.UE4.Objects.UObject;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// Thresholds an ability or effect compares an attribute against. Three routes reach one: a native character state
/// named in a tag requirement or a tag property, a Blueprint comparison of an attribute fraction against a scalable
/// float or a literal, and an <c>AbilityTask_WaitAttributeChange*Threshold</c> wait.
/// </summary>
public sealed class Thresholds
{
    public const string Section = "/Script/fellowship.CRCharacterBase";
    public const string ComparisonEnum = "EWaitAttributeChangeComparison";

    private const string StateSuffix = "FractionTheshold";
    private const string StateAttribute = "Health";
    private const string StateMaximum = "MaxHealth";
    private const string MaximumPrefix = "Max";
    private const string Divide = "Divide_";
    private const string ScalableRead = "GetScalableFloat";
    private const string Getter = "Get";
    private const string AttributeStruct = "GameplayAttribute";
    private const string RatioWait = "WaitForAttributeChangeRatioThreshold";
    private const string Wait = "WaitForAttributeChangeThreshold";
    private const string Removal = "Removal";
    private const int MaxDepth = 24;

    private static readonly string[] HeldFields = ["Require"];
    private static readonly string[] AbsentFields = ["Ignore", "Blocked"];
    private static readonly string[] TargetContainers = ["TargetTags", "TargetRequiredTags", "TargetBlockedTags"];

    private static readonly Dictionary<string, (string Comparison, string Flipped)> Comparisons = new(StringComparer.Ordinal)
    {
        ["Less_"] = ("LessThan", "GreaterThan"),
        ["LessEqual_"] = ("LessThanOrEqualTo", "GreaterThanOrEqualTo"),
        ["Greater_"] = ("GreaterThan", "LessThan"),
        ["GreaterEqual_"] = ("GreaterThanOrEqualTo", "LessThanOrEqualTo"),
    };

    private readonly IReadOnlyDictionary<string, double> _states;
    private readonly IReadOnlyDictionary<long, string> _comparisons;
    private readonly ConstantsTable _constants;

    public Thresholds(IReadOnlyDictionary<string, double> states, TypeMappings mappings, ConstantsTable constants)
    {
        _states = states;
        _comparisons = EnumOrdinals.Members(mappings, ComparisonEnum);
        _constants = constants;
    }

    /// <summary>Native state fractions, keyed by state: <c>LowHealthFractionTheshold</c> gives LowHealth.</summary>
    public IReadOnlyDictionary<string, double> States => _states;

    public static Thresholds Read(AbstractFileProvider provider, TypeMappings mappings, ConstantsTable constants)
    {
        var config = ConfigIni.Read(provider, ItemSettings.ConfigPath, Section);
        var states = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var key in config.Keys)
        {
            if (!key.EndsWith(StateSuffix, StringComparison.Ordinal) || key.Length == StateSuffix.Length) continue;

            states[key[..^StateSuffix.Length]] = double.TryParse(
                config.Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction)
                ? fraction
                : throw new InvalidOperationException(
                    $"[{Section}] in {ItemSettings.ConfigPath} sets {key} to '{config.Text(key)}', not a fraction.");
        }

        if (states.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{Section}] in {ItemSettings.ConfigPath} sets no *{StateSuffix}, so no character state has a " +
                "health fraction and every LowHealth or Healthy requirement reads with no threshold.");
        }

        return new Thresholds(states, mappings, constants);
    }

    public IReadOnlyList<Threshold> Of(UObject? cdo)
    {
        if (cdo?.Owner is not { } package) return [];

        var found = new List<Threshold>();
        var exports = package.GetExports().ToArray();

        foreach (var export in exports)
        {
            foreach (var property in PropertyWalk.Walk(export)) found.AddRange(Stated(property));
        }

        var bindings = CurveBindings.Collect(cdo, _constants);

        foreach (var body in exports.OfType<UStruct>().Where(export => export.ScriptBytecode is { Length: > 0 }))
        {
            found.AddRange(new Scope(body, bindings, _comparisons).Compared());
        }

        return [.. found.Distinct()];
    }

    private IEnumerable<Threshold> Stated(WalkedProperty property)
    {
        if (property.Value is FGameplayTag or FScriptStruct { StructType: IPropertyHolder } &&
            PropertyWalk.AsTags(property.Value) is null && PropertyWalk.AsString(property.Value) is { } single &&
            State(single) is { } held)
        {
            yield return held;
            yield break;
        }

        if (PropertyWalk.AsTags(property.Value) is not { Length: > 0 } tags) yield break;

        var absent = AbsentFields.Any(field => property.Name.Contains(field, StringComparison.Ordinal));
        if (!absent && !HeldFields.Any(field => property.Name.Contains(field, StringComparison.Ordinal))) yield break;
        if (property.Path.Contains(Removal, StringComparison.Ordinal)) absent = !absent;

        var target = property.Path.Split('.').Any(segment => TargetContainers.Contains(segment, StringComparer.Ordinal));

        foreach (var tag in tags)
        {
            if (State(tag) is not { } state) continue;
            yield return state with { Absent = absent ? true : null, Target = target ? true : null };
        }
    }

    private Threshold? State(string tag)
    {
        var dot = tag.LastIndexOf('.');
        if (dot <= 0 || !_states.TryGetValue(tag[(dot + 1)..], out var fraction)) return null;

        return new Threshold(StateAttribute, fraction) { Maximum = StateMaximum, State = tag[(dot + 1)..] };
    }

    private sealed class Scope(UStruct body, IReadOnlyList<CurveBinding> bindings, IReadOnlyDictionary<long, string> comparisons)
    {
        private readonly Dictionary<string, List<KismetExpression>> _locals = Locals(body);

        public IEnumerable<Threshold> Compared()
        {
            foreach (var statement in body.ScriptBytecode!)
            {
                foreach (var call in Calls(statement, 0))
                {
                    if (RowSinks.Calling(call) is not { } called) continue;

                    var parameters = RowSinks.Parameters(call);

                    if (called is RatioWait or Wait)
                    {
                        if (Waited(called, parameters) is { } waited) yield return waited;
                        continue;
                    }

                    if (Comparisons.FirstOrDefault(entry => called.StartsWith(entry.Key, StringComparison.Ordinal) &&
                                                            !called[entry.Key.Length..].Contains('_')) is not
                        { Key: not null, Value: var (comparison, flipped) } || parameters.Count != 2)
                    {
                        continue;
                    }

                    for (var side = 0; side < 2; side++)
                    {
                        if (Fraction(parameters[side], 0) is not var (attribute, maximum)) continue;
                        if (Limit(parameters[1 - side], 0) is not { } value || value <= 0) continue;

                        yield return new Threshold(attribute, value)
                        {
                            Maximum = maximum,
                            Comparison = side == 0 ? comparison : flipped,
                        };
                    }
                }
            }
        }

        private Threshold? Waited(string called, IReadOnlyList<KismetExpression> parameters)
        {
            var attributes = parameters.Select(Attribute).OfType<string>().ToArray();
            var at = parameters.ToList().FindIndex(parameter => parameter is EX_ByteConst);

            if (attributes.Length == 0 || at < 0 || at + 1 >= parameters.Count) return null;
            if (!comparisons.TryGetValue(((EX_ByteConst)parameters[at]).Value, out var comparison)) return null;
            if (Limit(parameters[at + 1], 0) is not { } value) return null;

            return called == RatioWait
                ? attributes.Length < 2 ? null : new Threshold(attributes[0], value) { Maximum = attributes[1], Comparison = comparison }
                : new Threshold(attributes[0], value) { Comparison = comparison };
        }

        private (string Attribute, string Maximum)? Fraction(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Fraction(cast.Target, depth + 1);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Select(value => Fraction(value, depth + 1))
                        .FirstOrDefault(found => found is not null);
            }

            if (RowSinks.Invoking(expression) is not { } call ||
                RowSinks.Calling(call)?.StartsWith(Divide, StringComparison.Ordinal) != true ||
                RowSinks.Parameters(call) is not [var numerator, var denominator])
            {
                return null;
            }

            if (Named(denominator, depth + 1) is not { } maximum ||
                !maximum.StartsWith(MaximumPrefix, StringComparison.Ordinal) || maximum.Length == MaximumPrefix.Length)
            {
                return null;
            }

            var attribute = maximum[MaximumPrefix.Length..];

            return Named(numerator, depth + 1) is { } read && read.Contains(attribute, StringComparison.Ordinal) &&
                   !read.StartsWith(MaximumPrefix, StringComparison.Ordinal)
                ? (attribute, maximum)
                : null;
        }

        private string? Named(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Named(cast.Target, depth + 1);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.TryGetValue(local, out var values)
                        ? values.Select(value => Named(value, depth + 1)).FirstOrDefault(found => found is not null)
                        : local;
                case EX_StructMemberContext or EX_InstanceVariable:
                    return DynamicTextFloats.Name(expression);
            }

            if (RowSinks.Invoking(expression) is not { } call || RowSinks.Calling(call) is not { } called) return null;

            if (RowSinks.Parameters(call).Select(Attribute).FirstOrDefault(found => found is not null) is { } attribute)
            {
                return attribute;
            }

            return called.StartsWith(Getter, StringComparison.Ordinal) && called.Length > Getter.Length &&
                   RowSinks.Parameters(call).Count == 0
                ? called[Getter.Length..]
                : null;
        }

        private double? Limit(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) return null;

            switch (expression)
            {
                case EX_Cast cast:
                    return Limit(cast.Target, depth + 1);
                case EX_DoubleConst constant:
                    return constant.Value;
                case EX_FloatConst constant:
                    return PropertyWalk.AsNumber(constant.Value);
                case EX_LocalVariable or EX_LocalOutVariable when DynamicTextFloats.Name(expression) is { } local:
                    return _locals.GetValueOrDefault(local)?.Select(value => Limit(value, depth + 1))
                        .FirstOrDefault(found => found is not null);
            }

            if (RowSinks.Invoking(expression) is not { } call ||
                RowSinks.Calling(call)?.StartsWith(ScalableRead, StringComparison.Ordinal) != true)
            {
                return null;
            }

            return RowSinks.Parameters(call)
                .Select(DynamicTextFloats.Name)
                .OfType<string>()
                .Select(property => bindings.FirstOrDefault(binding => binding.Property.Equals(property, StringComparison.Ordinal))?.Value)
                .FirstOrDefault(found => found is not null);
        }

        private static string? Attribute(KismetExpression? expression) =>
            expression is EX_StructConst constant &&
            constant.Struct?.Name.EndsWith(AttributeStruct, StringComparison.Ordinal) == true &&
            constant.Properties.OfType<EX_StringConst>().FirstOrDefault() is { Value.Length: > 0 } name
                ? name.Value
                : null;

        private static Dictionary<string, List<KismetExpression>> Locals(UStruct body)
        {
            var locals = new Dictionary<string, List<KismetExpression>>(StringComparer.Ordinal);

            foreach (var statement in body.ScriptBytecode ?? [])
            {
                var (variable, value) = statement switch
                {
                    EX_Let { Variable: { } held, Assignment: { } assigned } => (held, assigned),
                    EX_LetObj { Variable: { } held, Assignment: { } assigned } => (held, assigned),
                    EX_LetBool { Variable: { } held, Assignment: { } assigned } => (held, assigned),
                    _ => (null, null),
                };

                if (variable is null or EX_InstanceVariable || value is null) continue;
                if (DynamicTextFloats.Name(variable) is not { } local) continue;

                if (!locals.TryGetValue(local, out var values)) locals[local] = values = [];
                values.Add(value);
            }

            return locals;
        }

        private static IEnumerable<KismetExpression> Calls(KismetExpression? expression, int depth)
        {
            if (expression is null || depth > MaxDepth) yield break;

            if (RowSinks.Invoking(expression) is { } call)
            {
                yield return call;

                foreach (var parameter in RowSinks.Parameters(call))
                {
                    foreach (var nested in Calls(parameter, depth + 1)) yield return nested;
                }

                yield break;
            }

            foreach (var child in RowSinks.Children(expression))
            {
                foreach (var nested in Calls(child, depth + 1)) yield return nested;
            }
        }
    }
}
