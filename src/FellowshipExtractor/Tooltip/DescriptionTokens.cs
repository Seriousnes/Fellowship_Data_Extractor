using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The {Token} substitutions a description carries. A token names the gameplay effect that
/// produces its number, so each one resolves to the attributes that effect captures and the
/// coefficients it multiplies them by, which together are what the displayed number is made of.
/// </summary>
public static class DescriptionTokens
{
    public static Dictionary<string, Placeholder> Resolve(
        IPropertyHolder row,
        AbstractFileProvider provider,
        ConstantsTable constants,
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

            resolved[name] = ResolveOne(token, provider, constants, tuning, ownAsset);
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

    private static IEnumerable<string> Tokens(string description)
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
        string? tuning,
        string? ownAsset)
    {
        // The attribute the token itself names, when it names one rather than reaching an effect.
        var direct = PropertyWalk.GetStruct(token, "Attribute") is { } attribute
            ? PropertyWalk.GetString(attribute, "AttributeName")
            : null;

        var bare = new Placeholder { ScalesOff = direct is null ? null : [direct] };

        if (PropertyWalk.GetString(token, "Object") is not { } objectPath) return bare;

        // 44 of the 229 tokens that name an object name the ability that owns them, and native code
        // works the number out from the ability's own tuning. Reading that as though it were an
        // effect claims every constant on the ability as the token's own, so the token is left
        // bare and the numbers stay in the entity's constants, which is where they belong.
        if (IsSelf(objectPath, ownAsset)) return bare with { FromOwnConstants = true };

        // The token points at the gameplay effect that produces the number, and it is read exactly
        // as an effect the ability references directly is, so both routes report the same thing.
        var effect = GameplayEffects.Resolve(provider, constants, objectPath);

        var scaling = new SortedSet<string>(StringComparer.Ordinal);
        if (direct is not null) scaling.Add(direct);
        foreach (var captured in effect.CapturedAttributes ?? []) scaling.Add(captured);

        var numbers = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var binding in effect.Constants ?? []) Numbers.Add(numbers, binding, tuning);

        return new Placeholder
        {
            ScalesOff = scaling.Count == 0 ? null : [.. scaling],
            Constants = numbers.Count == 0 ? null : numbers,
        };
    }
}
