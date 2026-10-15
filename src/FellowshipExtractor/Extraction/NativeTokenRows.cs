namespace FellowshipExtractor.Extraction;

public static class NativeTokenRows
{
    private static readonly Dictionary<string, string> Rows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GA_Vigor_SingleTargetAbsorb/LuminousDmgRed"] = "Vigor.Absorbs.DamageReduction.BaseValue",
    };

    public static double? Value(string? assetName, string token, ConstantsTable constants)
    {
        if (assetName is null) return null;

        var asset = assetName.EndsWith("_C", StringComparison.Ordinal) ? assetName[..^2] : assetName;

        return Rows.TryGetValue($"{asset}/{token}", out var row)
            ? constants.Lookup(row).Record?.Value
            : null;
    }
}
