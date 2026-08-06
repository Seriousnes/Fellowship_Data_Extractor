using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

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
        string? ownAsset)
    {
        var resolved = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        if (PropertyWalk.GetStruct(row, "DynamicDescription") is not { } dynamic) return resolved;
        if (PropertyWalk.Get(dynamic, "Tokens")?.GenericValue is not UScriptArray tokens) return resolved;

        foreach (var entry in tokens.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder token }) continue;
            if (PropertyWalk.GetString(token, "TokenName") is not { } name) continue;

            resolved[name] = ResolveOne(token, provider, constants, presets, tuning, ownAsset);
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
        IPropertyHolder token,
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string? tuning,
        string? ownAsset)
    {
        var direct = PropertyWalk.GetStruct(token, "Attribute") is { } attribute
            ? PropertyWalk.GetString(attribute, "AttributeName")
            : null;

        var quantity = TokenCalculations.Of(PropertyWalk.GetString(token, "Calculation"));

        var declared = new Placeholder
        {
            Quantity = quantity,
            Percent = Flag(token, "ShowPercent") ? true : null,
            ShowDamageType = Flag(token, "ShowDamageType") ? true : null,

            Unit = Flag(token, "ShowUnit") && quantity is TokenQuantity.Period or TokenQuantity.Duration
                ? Seconds
                : null,
        };

        var bare = declared with { ScalesOff = direct is null ? null : [direct] };

        if (OwnConstant(token, constants) is { } own)
        {
            return Made(declared, direct, null, [own], tuning);
        }

        if (PropertyWalk.GetString(token, "Object") is not { } objectPath) return bare;

        if (IsSelf(objectPath, ownAsset)) return bare with { FromOwnConstants = true };

        var effect = GameplayEffects.Resolve(provider, constants, presets, objectPath);
        declared = declared with
        {
            PresetSpread = effect.Spread,
            IgnoresHaste = effect.TickrateIgnoresHaste ? true : null,
        };

        var selected = PropertyWalk.GetString(token, "Type") == TokenCalculations.EffectType
            ? TokenCalculations.Select(effect.Constants ?? [], quantity)
            : effect.Constants ?? [];

        return Made(declared, direct, effect.CapturedAttributes, selected, tuning);
    }

    private static bool Flag(IPropertyHolder token, string name) =>
        PropertyWalk.Get(token, name)?.GenericValue is true;

    private static CurveBinding? OwnConstant(IPropertyHolder token, ConstantsTable constants) =>
        CurveBindings.Collect(token, constants)
            .FirstOrDefault(b => b.Property.Equals(TokenMagnitude, StringComparison.Ordinal));

    private static Placeholder Made(
        Placeholder declared,
        string? direct,
        IReadOnlyList<string>? captured,
        IReadOnlyList<CurveBinding> bindings,
        string? tuning)
    {
        var scaling = new SortedSet<string>(StringComparer.Ordinal);
        if (direct is not null) scaling.Add(direct);
        foreach (var attribute in captured ?? []) scaling.Add(attribute);

        var numbers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var binding in bindings) Numbers.Add(numbers, binding, tuning);

        if (scaling.Count == 0 && numbers.Count == 0) return declared;

        return declared with
        {
            ScalesOff = scaling.Count == 0 ? null : [.. scaling],
            Constants = numbers.Count == 0 ? null : numbers,
        };
    }
}
