using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public static class DescriptionTokens
{
    private const string TokenMagnitude = "Magnitude.ScalableFloatMagnitude";
    private const string Seconds = "seconds";

    public static Dictionary<string, Placeholder> Resolve(
        IPropertyHolder row,
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string? tuning,
        string? ownAsset,
        ICollection<string> unbindableSpreadRows,
        HeroNames heroes)
    {
        var resolved = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        if (PropertyWalk.GetStruct(row, "DynamicDescription") is not { } dynamic) return resolved;
        if (PropertyWalk.Get(dynamic, "Tokens")?.GenericValue is not UScriptArray tokens) return resolved;

        foreach (var entry in tokens.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder token }) continue;
            if (PropertyWalk.GetString(token, "TokenName") is not { } name) continue;

            resolved[name] = ResolveOne(
                name, token, provider, constants, presets, tuning, ownAsset, unbindableSpreadRows, heroes);
        }

        return resolved;
    }

    public static Dictionary<string, Placeholder> Align(
        Dictionary<string, Placeholder> placeholders, string? description)
    {
        if (description is null) return placeholders;

        foreach (var written in Tokens(description))
        {
            if (placeholders.ContainsKey(written)) continue;

            var declared = placeholders.Keys.FirstOrDefault(
                k => k.Equals(written, StringComparison.OrdinalIgnoreCase));
            if (declared is null) continue;

            placeholders[written] = placeholders[declared];
            placeholders.Remove(declared);
        }

        return placeholders;
    }

    public static string? Renamed(
        Dictionary<string, Placeholder> placeholders, string? description, HeroNames heroes)
    {
        foreach (var written in placeholders.Keys.ToArray())
        {
            var shipped = heroes.Renamed(written);
            if (shipped.Equals(written, StringComparison.Ordinal)) continue;

            placeholders[shipped] = placeholders[written];
            placeholders.Remove(written);

            description = description?.Replace($"{{{written}}}", $"{{{shipped}}}", StringComparison.Ordinal);
        }

        return description;
    }

    private static bool IsSelf(string objectPath, string? ownAsset)
    {
        if (ownAsset is null) return false;

        var named = AbilityPresentationIndex.AssetName(objectPath);
        if (named.EndsWith("_C", StringComparison.Ordinal)) named = named[..^2];

        var own = ownAsset.EndsWith("_C", StringComparison.Ordinal) ? ownAsset[..^2] : ownAsset;
        return named.Equals(own, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> Tokens(string description)
    {
        var from = 0;
        while (description.IndexOf('{', from) is var open and >= 0)
        {
            var close = description.IndexOf('}', open + 1);
            if (close < 0) yield break;

            yield return description[(open + 1)..close];
            from = close + 1;
        }
    }

    private static Placeholder ResolveOne(
        string name,
        IPropertyHolder token,
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string? tuning,
        string? ownAsset,
        ICollection<string> unbindableSpreadRows,
        HeroNames heroes)
    {
        var direct = PropertyWalk.GetStruct(token, "Attribute") is { } attribute
            ? PropertyWalk.GetString(attribute, "AttributeName")
            : null;

        var objectPath = PropertyWalk.GetString(token, "Object");
        var self = objectPath is not null && IsSelf(objectPath, ownAsset);

        var quantity = TokenCalculations.Of(Ordinal(token, "Calculation"));
        if (self && quantity == TokenQuantity.Unknown) quantity = TokenCalculations.ForName(name);

        var declared = new Placeholder
        {
            Quantity = quantity,
            Percent = Flag(token, "ShowPercent") ? true : null,
            Multiplies = Multiplied(token) ? true : null,
            ShowDamageType = Flag(token, "ShowDamageType") ? true : null,

            Unit = Flag(token, "ShowUnit") && quantity is TokenQuantity.Period or TokenQuantity.Duration
                ? Seconds
                : null,
        };

        var bare = declared with { ScalesOff = direct is null ? null : [direct] };

        if (OwnConstant(token, constants) is { } own)
        {
            return Made(declared, direct, null, [own], tuning, heroes);
        }

        if (objectPath is null) return bare;

        if (self)
        {
            return bare with
            {
                FromOwnConstants = true,
                DynamicTextFloatIdentifier = Identifier(token),
            };
        }

        var effect = GameplayEffects.Resolve(provider, constants, presets, objectPath, unbindableSpreadRows);
        declared = declared with
        {
            PresetSpread = effect.Spread,
            PresetSpreadOverridden = effect.SpreadOverridden,
            IgnoresHaste = effect.TickrateIgnoresHaste ? true : null,
        };

        var selected = Ordinal(token, "Type") == TokenCalculations.EffectType
            ? TokenCalculations.Select(effect.Constants ?? [], quantity)
            : effect.Constants ?? [];

        if (selected.Any(binding => binding.Multiplies)) declared = declared with { Multiplies = true };

        return Made(declared, direct, effect.CapturedAttributes, selected, tuning, heroes);
    }

    private static bool Flag(IPropertyHolder token, string name) =>
        PropertyWalk.Get(token, name)?.GenericValue is true;

    private static int? Ordinal(IPropertyHolder token, string name) =>
        PropertyWalk.AsOrdinal(PropertyWalk.Get(token, name)?.GenericValue);

    private static bool Multiplied(IPropertyHolder token) =>
        GameplayEffects.Multiplies(PropertyWalk.AsOrdinal(PropertyWalk.Get(token, "ModifierOp")?.GenericValue));

    private static int? Identifier(IPropertyHolder token) =>
        PropertyWalk.AsNumber(PropertyWalk.Get(token, "AbilityDynamicTextFloatIdentifier")?.GenericValue) is { } value
            ? (int)value
            : null;

    private static CurveBinding? OwnConstant(IPropertyHolder token, ConstantsTable constants) =>
        CurveBindings.Collect(token, constants)
            .FirstOrDefault(b => b.Property.Equals(TokenMagnitude, StringComparison.Ordinal));

    private static Placeholder Made(
        Placeholder declared,
        string? direct,
        IReadOnlyList<string>? captured,
        IReadOnlyList<CurveBinding> bindings,
        string? tuning,
        HeroNames heroes)
    {
        var scaling = new SortedSet<string>(StringComparer.Ordinal);
        if (direct is not null) scaling.Add(direct);
        foreach (var attribute in captured ?? []) scaling.Add(attribute);

        var numbers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var binding in bindings) Numbers.Add(numbers, binding, tuning, heroes);

        if (scaling.Count == 0 && numbers.Count == 0) return declared;

        return declared with
        {
            ScalesOff = scaling.Count == 0 ? null : [.. scaling],
            Constants = numbers.Count == 0 ? null : numbers,
        };
    }
}
