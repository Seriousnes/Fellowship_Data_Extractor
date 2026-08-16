using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record EffectRef
{
    public IReadOnlyList<string>? CapturedAttributes { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }

    public double? Spread { get; init; }

    public bool SpreadOverridden { get; init; }

    public bool TickrateIgnoresHaste { get; init; }
}

public static class GameplayEffects
{
    private const string AttributeField = "AttributeName";
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";

    public static IEnumerable<(string Name, string Path)> ReferencedIn(
        UObject asset, IReadOnlySet<string> effectNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in PropertyWalk.Walk(asset))
        {
            if (PropertyWalk.AsString(property.Value) is not { } path) continue;

            if (!path.Contains('/') || path.StartsWith(ScriptRoot, StringComparison.OrdinalIgnoreCase)) continue;

            var name = AbilityPresentationIndex.AssetName(path);
            if (name.EndsWith(ClassSuffix, StringComparison.Ordinal)) name = name[..^ClassSuffix.Length];

            if (!effectNames.Contains(name)) continue;
            if (!seen.Add(path)) continue;

            yield return (name, path);
        }
    }

    public static IEnumerable<(string Name, string Path, bool Widened)> Reached(
        UObject asset, IReadOnlySet<string> effectNames) =>
        Union(asset, effectNames, AppliedEffects.Referenced);

    public static IEnumerable<(string Name, string Path, bool Widened)> Applying(
        UObject asset, IReadOnlySet<string> effectNames) =>
        Union(asset, effectNames, AppliedEffects.Applying);

    private static IEnumerable<(string Name, string Path, bool Widened)> Union(
        UObject asset,
        IReadOnlySet<string> effectNames,
        Func<IPackage, IReadOnlySet<string>, IReadOnlyList<(string Name, string Path)>> wider)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, path) in ReferencedIn(asset, effectNames))
        {
            seen.Add(name);

            yield return (name, path, false);
        }

        if (asset.Owner is not { } package) yield break;

        foreach (var (name, path) in wider(package, effectNames))
        {
            if (seen.Add(name)) yield return (name, path, true);
        }
    }

    public static EffectRef Resolve(
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string objectPath,
        ICollection<string> unbindableSpreadRows)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return new EffectRef();
        }

        var effect = package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
        if (effect is null) return new EffectRef();

        var custom = MagnitudeCalculations.Of(effect, provider, constants);
        var (band, unbindable) = SpreadOverride(effect, constants);

        if (unbindable is not null) unbindableSpreadRows.Add(unbindable);

        return new EffectRef
        {
            CapturedAttributes = Combined(Captures(effect), custom.Captures),
            Constants = Prefixed(custom.Duration, Collected(effect, constants)),
            Spread = band ?? presets.Of(effect),
            SpreadOverridden = band is not null,
            TickrateIgnoresHaste = IgnoresHaste(effect),
        };
    }

    private const string ScopedModifiersField = "CalculationModifiers";
    private const string TransientIdentifierField = "TransientAggregatorIdentifier";
    private const string TagNameField = "TagName";
    private const string SpreadTag = "SetByCallerTags.Spread";
    private const string AggregatorTypeField = "AggregatorType";
    private const string TransientAggregator = "EGameplayEffectScopedModifierAggregatorType::Transient";
    private const string OverrideOp = "EGameplayModOp::Override";
    private const string MagnitudeField = "ModifierMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string ScalableFloatCalculation = "EGameplayEffectMagnitudeCalculation::ScalableFloat";
    private const string ScalableFloatField = "ScalableFloatMagnitude";
    private const string CurveHandleField = "Curve";
    private const string RowNameField = "RowName";
    private const string ScaleField = "Value";

    public static (double? Spread, string? Unbindable) SpreadOverride(UObject effect, ConstantsTable constants)
    {
        foreach (var property in PropertyWalk.Walk(effect))
        {
            if (!property.Name.Equals(ScopedModifiersField, StringComparison.Ordinal)) continue;
            if (property.Value is not FScriptStruct { StructType: IPropertyHolder modifier }) continue;
            if (Overriding(modifier) is not { } scalable) continue;

            var scale = PropertyWalk.AsNumber(PropertyWalk.Get(scalable, ScaleField)?.GenericValue) ?? 0;

            if (PropertyWalk.GetStruct(scalable, CurveHandleField) is not { } curve) return (scale, null);
            if (PropertyWalk.GetString(curve, RowNameField) is not { } rowName) return (scale, null);

            return constants.Lookup(rowName).Record?.Value is { } declared
                ? (Math.Round(scale * declared, 10), null)
                : (null, rowName);
        }

        return (null, null);
    }

    private static IPropertyHolder? Overriding(IPropertyHolder modifier)
    {
        if (PropertyWalk.GetStruct(modifier, TransientIdentifierField) is not { } identifier) return null;
        if (PropertyWalk.GetString(identifier, TagNameField) != SpreadTag) return null;
        if (Named(modifier, AggregatorTypeField) != TransientAggregator) return null;
        if (Named(modifier, ModifierOpField) != OverrideOp) return null;
        if (PropertyWalk.GetStruct(modifier, MagnitudeField) is not { } magnitude) return null;
        if (Named(magnitude, CalculationTypeField) != ScalableFloatCalculation) return null;

        return PropertyWalk.GetStruct(magnitude, ScalableFloatField);
    }

    private static string? Named(IPropertyHolder holder, string field) =>
        PropertyWalk.AsString(PropertyWalk.Get(holder, field)?.GenericValue);

    private static IReadOnlyList<string>? Combined(IReadOnlyList<string>? own, IReadOnlyList<string> computed)
    {
        if (computed.Count == 0) return own;
        if (own is null) return computed;

        return [.. own.Concat(computed).Distinct(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<CurveBinding>? Prefixed(CurveBinding? computed, IReadOnlyList<CurveBinding>? own)
    {
        if (computed is null) return own;

        return [computed, .. own ?? []];
    }

    public const int Multiplicitive = 1;

    private const string ModifiersField = "Modifiers";
    private const string ModifierOpField = "ModifierOp";

    private static IReadOnlyList<CurveBinding>? Collected(UObject effect, ConstantsTable constants)
    {
        if (CurveBindings.Collect(effect, constants) is not { Count: > 0 } bindings) return null;
        if (Multiplied(effect) is not { Count: > 0 } scopes) return bindings;

        return
        [
            .. bindings.Select(binding =>
                scopes.Any(scope => binding.Property.StartsWith(scope, StringComparison.Ordinal))
                    ? binding with { Multiplies = true }
                    : binding),
        ];
    }

    private static IReadOnlyList<string> Multiplied(UObject effect)
    {
        if (PropertyWalk.Get(effect, ModifiersField)?.GenericValue is not UScriptArray modifiers) return [];

        var scopes = new List<string>();

        for (var at = 0; at < modifiers.Properties.Count; at++)
        {
            if (modifiers.Properties[at]?.GenericValue is not FScriptStruct { StructType: IPropertyHolder modifier })
            {
                continue;
            }

            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(modifier, ModifierOpField)?.GenericValue) == Multiplicitive)
            {
                scopes.Add($"{ModifiersField}[{at}].");
            }
        }

        return scopes;
    }

    private const string HasteExemptTag = "EffectLogic.TickrateNotAffectedByHasteExclusively";

    public static bool IgnoresHaste(UObject effect) => Declares(effect, HasteExemptTag);

    private static bool Declares(UObject effect, string tag) =>
        PropertyWalk.Walk(effect)
            .Select(p => PropertyWalk.AsTags(p.Value))
            .OfType<string[]>()
            .Any(tags => tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));

    public static IReadOnlyList<string>? Captures(UObject effect)
    {
        var attributes = PropertyWalk.Walk(effect)
            .Where(p => p.Name == AttributeField)
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return attributes.Length == 0 ? null : attributes;
    }
}
