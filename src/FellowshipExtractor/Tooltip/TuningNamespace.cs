namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The prefix an entity's tuning rows share. GA_Firemage_CastedPlacedAoe reaches
/// Firemage.CastedPlacedAoe.Radius, Firemage.CastedPlacedAoe.Cooldown and a dozen more, so
/// "Firemage.CastedPlacedAoe" names the whole set.
///
/// The candidate comes from the asset name, but a name is only a convention, so it counts only
/// when the shipped tables confirm rows actually sit under it. An asset whose name confirms
/// nothing simply has no namespace and keeps its full row names.
/// </summary>
public static class TuningNamespace
{
    private const string ClassSuffix = "_C";

    /// <summary>The namespace an asset's rows sit under, or null when the tables confirm none.</summary>
    public static string? For(string? assetName, ConstantsTable constants)
    {
        if (assetName is null) return null;

        var name = assetName.EndsWith(ClassSuffix, StringComparison.Ordinal)
            ? assetName[..^ClassSuffix.Length]
            : assetName;

        var segments = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return null;

        // The whole name first, so an asset that is already namespace-shaped is not mistaken for
        // one carrying a GA_ or GE_ type prefix and stripped a segment too far.
        var whole = string.Join('.', segments);
        if (constants.IsNamespace(whole)) return whole;

        var withoutType = string.Join('.', segments[1..]);
        return segments.Length > 2 && constants.IsNamespace(withoutType) ? withoutType : null;
    }

    /// <summary>
    /// The tuning namespace behind a talent's gameplay tag. A talent's rows do not sit under its own
    /// asset name - CAA_Talent_Rime_GlacialAssault names nothing in the tables - they sit under the
    /// ability the talent modifies, with a Talent segment before the talent's own name. The tag it
    /// grants carries both halves: Talents.Rime.CastedProjectileDamageOrbSpender.InstantAndBonusDamage
    /// becomes Rime.CastedProjectileDamageOrbSpender.Talent.InstantAndBonusDamage, which is where
    /// the 40% damage bonus in Glacial Assault's own text lives.
    /// </summary>
    public static string? ForTalentTag(string tag, ConstantsTable constants)
    {
        const string prefix = "Talents.";
        if (!tag.StartsWith(prefix, StringComparison.Ordinal)) return null;

        var parts = tag[prefix.Length..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var candidate = string.Join('.', [.. parts[..^1], "Talent", parts[^1]]);
        return constants.IsNamespace(candidate) ? candidate : null;
    }

    /// <summary>The row name as the entity sees it: its own prefix dropped, foreign rows left whole.</summary>
    public static string Relative(string rowName, string? tuningNamespace) =>
        tuningNamespace is not null &&
        rowName.Length > tuningNamespace.Length + 1 &&
        rowName.StartsWith(tuningNamespace, StringComparison.OrdinalIgnoreCase) &&
        rowName[tuningNamespace.Length] == '.'
            ? rowName[(tuningNamespace.Length + 1)..]
            : rowName;
}
