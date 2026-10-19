using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed class DungeonDifficulties
{
    public const string RowStructure = "/Script/fellowship.CRDifficultyToLevelRow";

    private const string LevelColumnPrefix = "Level";

    private readonly Dictionary<string, SortedSet<int>> _byLevel = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byLevel.Count;

    public DungeonDifficulties(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (!int.TryParse(rowName.Text, out var difficulty) || difficulty < 1) continue;

                foreach (var column in row.Properties)
                {
                    if (!column.Name.Text.StartsWith(LevelColumnPrefix, StringComparison.Ordinal)) continue;
                    if (PropertyWalk.AsString(column.Tag?.GenericValue) is not { } level) continue;

                    if (!_byLevel.TryGetValue(level, out var difficulties))
                    {
                        _byLevel[level] = difficulties = [];
                    }

                    difficulties.Add(difficulty);
                }
            }
        }
    }

    public IReadOnlyList<int>? For(string levelTag) =>
        _byLevel.TryGetValue(levelTag, out var difficulties) ? [.. difficulties] : null;
}
