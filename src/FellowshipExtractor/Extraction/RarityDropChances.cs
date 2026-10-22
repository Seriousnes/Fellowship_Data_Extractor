using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed class RarityDropChances
{
    public const string ConfigPath = "fellowship/Plugins/GameFeatures/DynamicLootSystem/Config/DynamicLootSystem.ini";
    public const string Section = "/Script/DynamicLootSystemRuntime.CRDynamicItemSettings";
    public const string TableKey = "RarityDropChanceCurveTable";

    private readonly HashSet<int> _dropping = [];

    public string Table { get; }

    public IReadOnlyCollection<int> Dropping => _dropping;

    public RarityDropChances(AbstractFileProvider provider, RarityNames rarities)
    {
        var path = ConfigIni.Read(provider, ConfigPath, Section).Text(TableKey).Trim('"');
        Table = AbilityPresentationIndex.AssetName(path);

        if (!provider.TryLoadPackageObject<UCurveTable>(AbilityPresentationIndex.PackagePath(path), out var curves))
        {
            throw new InvalidOperationException(
                $"{Section}.{TableKey} names {path}, and no curve table loads from it, so nothing states which " +
                "rarities an item rolls.");
        }

        var stated = new HashSet<int>();

        foreach (var (rowName, row) in curves.RowMap)
        {
            if (rarities.Tier(rowName.Text) is not { } tier)
            {
                throw new InvalidOperationException(
                    $"{Table} has a row {rowName.Text}, and the rarity ladder has no tier by that name.");
            }

            stated.Add(tier);

            if (CurveRows.Keys(row).Any(key => key.Value > 0)) _dropping.Add(tier);
        }

        if (rarities.Tiers.Where(tier => !stated.Contains(tier)).ToArray() is { Length: > 0 } unstated)
        {
            throw new InvalidOperationException(
                $"{Table} states no drop chance for {string.Join(", ", unstated.Select(tier => rarities.Label(tier)))}.");
        }
    }

    public IReadOnlyList<int> Rolled(IReadOnlyList<int> listed) =>
        listed.Any(_dropping.Contains) ? [.. listed.Where(_dropping.Contains)] : listed;
}
