namespace FellowshipExtractor.Extraction;

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

        var withoutType = segments[1..];

        for (var kept = 1; kept < withoutType.Length; kept++)
        {
            var candidate = string.Join(
                '.', (string[])[string.Join('_', withoutType[..kept]), .. withoutType[kept..]]);

            if (constants.IsNamespace(candidate)) return candidate;
        }

        return null;
    }

    public static string? FromBindings(IEnumerable<CurveBinding> bindings, ConstantsTable constants)
    {
        var under = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var binding in bindings)
        {
            var separator = binding.RowName.LastIndexOf('.');
            if (separator <= 0) continue;

            var prefix = binding.RowName[..separator];
            if (!constants.IsNamespace(prefix)) continue;

            under[prefix] = under.GetValueOrDefault(prefix) + 1;
        }

        if (under.Count == 0) return null;

        var most = under.Max(scope => scope.Value);
        var leading = under.Where(scope => scope.Value == most).Select(scope => scope.Key).ToArray();

        return leading.Length == 1 ? leading[0] : null;
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
