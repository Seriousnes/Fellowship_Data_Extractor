namespace FellowshipExtractor.Tooltip;

public static class TuningNamespace
{
    private const string ClassSuffix = "_C";

    public static string? For(string? assetName, ConstantsTable constants)
    {
        if (assetName is null) return null;

        var name = assetName.EndsWith(ClassSuffix, StringComparison.Ordinal)
            ? assetName[..^ClassSuffix.Length]
            : assetName;

        var segments = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return null;

        var whole = string.Join('.', segments);
        if (constants.IsNamespace(whole)) return whole;

        var withoutType = string.Join('.', segments[1..]);
        return segments.Length > 2 && constants.IsNamespace(withoutType) ? withoutType : null;
    }

    public static string? ForTalentTag(string tag, ConstantsTable constants)
    {
        const string prefix = "Talents.";
        if (!tag.StartsWith(prefix, StringComparison.Ordinal)) return null;

        var parts = tag[prefix.Length..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        var candidate = string.Join('.', [.. parts[..^1], "Talent", parts[^1]]);
        return constants.IsNamespace(candidate) ? candidate : null;
    }

    public static string Relative(string rowName, string? tuningNamespace) =>
        tuningNamespace is not null &&
        rowName.Length > tuningNamespace.Length + 1 &&
        rowName.StartsWith(tuningNamespace, StringComparison.OrdinalIgnoreCase) &&
        rowName[tuningNamespace.Length] == '.'
            ? rowName[(tuningNamespace.Length + 1)..]
            : rowName;
}
