using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record EffectRef
{
    public IReadOnlyList<string>? CapturedAttributes { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }

    public double? Spread { get; init; }

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

    public static EffectRef Resolve(
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string objectPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return new EffectRef();
        }

        var effect = package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
        if (effect is null) return new EffectRef();

        var custom = MagnitudeCalculations.Of(effect, provider, constants);

        return new EffectRef
        {
            CapturedAttributes = Combined(Captures(effect), custom.Captures),
            Constants = Prefixed(custom.Duration, Collected(effect, constants)),
            Spread = presets.Of(effect),
            TickrateIgnoresHaste = IgnoresHaste(effect),
        };
    }

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
