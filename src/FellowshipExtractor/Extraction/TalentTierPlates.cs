using CUE4Parse.FileProvider;

namespace FellowshipExtractor.Extraction;

public sealed class TalentTierPlates
{
    public const string Family = "T_Backgrounds_TalentTierUnlocked_";

    private const string Suffix = "_2026";

    private const string Extension = ".uasset";

    public IReadOnlyDictionary<int, string> ByRow { get; }

    public TalentTierPlates(AbstractFileProvider provider)
    {
        var plates = new SortedDictionary<int, string>();

        foreach (var file in provider.Files.Values)
        {
            if (!file.Path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;

            var name = Path.GetFileNameWithoutExtension(file.Path);

            if (!name.StartsWith(Family, StringComparison.Ordinal)) continue;
            if (!name.EndsWith(Suffix, StringComparison.Ordinal)) continue;

            var tier = name[Family.Length..^Suffix.Length];

            if (int.TryParse(tier, out var row)) plates[row] = name;
        }

        if (plates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {Family}<tier>{Suffix} texture is in the build, so no talent row has the numbered plate " +
                "the tree draws at its left edge.");
        }

        ByRow = plates;
    }
}
