using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

public static class EffectUiData
{
    private const string ClassPrefix = "GameplayEffectUIData";

    public static UObject? Find(UObject? cdo)
    {
        foreach (var export in cdo?.Owner?.GetExports() ?? [])
        {
            if (export.ExportType.StartsWith(ClassPrefix, StringComparison.Ordinal)) return export;
        }

        return null;
    }
}
