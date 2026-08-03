using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The {Token} substitutions a description carries.
///
/// A token declares three things and all three are read. Type says whether the number sits on the
/// token itself or on an object it names; Object names that object; and Calculation says which of
/// the object's several numbers is the one meant. Reading Object without Calculation hands every
/// token pointing at an effect that effect's whole tuning, so a heal and the period it ticks on
/// arrive indistinguishable. <see cref="TokenCalculations"/> holds what the ordinals mean and the
/// corpus evidence for each.
/// </summary>
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

    /// <summary>
    /// Re-keys a placeholder to the spelling the description uses. One row in the build declares
    /// "Duration" and writes "{duration}", so a reader matching the two exactly would find nothing.
    /// </summary>
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

    /// <summary>Whether a token's object is the asset the token's own row describes.</summary>
    private static bool IsSelf(string objectPath, string? ownAsset)
    {
        if (ownAsset is null) return false;

        var named = AbilityPresentationIndex.AssetName(objectPath);
        if (named.EndsWith("_C", StringComparison.Ordinal)) named = named[..^2];

        var own = ownAsset.EndsWith("_C", StringComparison.Ordinal) ? ownAsset[..^2] : ownAsset;
        return named.Equals(own, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The {Token} names a piece of text writes, in the order it writes them.</summary>
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
        // The attribute the token itself names, when it names one rather than reaching an effect.
        var direct = PropertyWalk.GetStruct(token, "Attribute") is { } attribute
            ? PropertyWalk.GetString(attribute, "AttributeName")
            : null;

        // Read for every token, not only the ones selecting off an effect: a token naming its own
        // ability carries the same ordinal and it says the same thing there.
        var quantity = TokenCalculations.Of(PropertyWalk.GetString(token, "Calculation"));

        var declared = new Placeholder
        {
            Quantity = quantity,
            Percent = Flag(token, "ShowPercent") ? true : null,
            ShowDamageType = Flag(token, "ShowDamageType") ? true : null,

            // A unit the sentence omits is only recoverable where the ordinal says what the number
            // is. Every token asking for one names a period or a duration, and both are seconds.
            Unit = Flag(token, "ShowUnit") && quantity is TokenQuantity.Period or TokenQuantity.Duration
                ? Seconds
                : null,
        };

        var bare = declared with { ScalesOff = direct is null ? null : [direct] };

        // A constant named on the token is the token's own answer and needs no object. All 23
        // Type 3 tokens are named this way, and four Type 0 tokens name one as well as an effect,
        // where the row is the same coefficient the effect would have been read for.
        if (OwnConstant(token, constants) is { } own)
        {
            return Made(declared, direct, null, [own], tuning);
        }

        if (PropertyWalk.GetString(token, "Object") is not { } objectPath) return bare;

        // 206 of the 229 tokens name an object and 44 of those name the ability that owns them,
        // where native code works the number out from the ability's own tuning. Reading that as
        // though it were an effect claims every constant on the ability as the token's own, so the
        // token is left bare and the numbers stay in the entity's constants, where they belong.
        if (IsSelf(objectPath, ownAsset)) return bare with { FromOwnConstants = true };

        // The token points at the gameplay effect that produces the number, and it is read exactly
        // as an effect the ability references directly is, so both routes report the same thing.
        var effect = GameplayEffects.Resolve(provider, constants, presets, objectPath);
        declared = declared with { PresetSpread = effect.Spread };

        // Only a Type 0 token's Calculation was shown to select off an effect. Types 2 and 4 name
        // an ability, and the five of those that name someone else's keep the whole-object reading.
        var selected = PropertyWalk.GetString(token, "Type") == TokenCalculations.EffectType
            ? TokenCalculations.Select(effect.Constants ?? [], quantity)
            : effect.Constants ?? [];

        return Made(declared, direct, effect.CapturedAttributes, selected, tuning);
    }

    /// <summary>
    /// A boolean the token declares. Unversioned properties omit a field left at its default, so a
    /// flag that is off is usually not written at all.
    /// </summary>
    private static bool Flag(IPropertyHolder token, string name) =>
        PropertyWalk.Get(token, name)?.GenericValue is true;

    /// <summary>The constant an FScalableFloat on the token itself names, when it names one.</summary>
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
