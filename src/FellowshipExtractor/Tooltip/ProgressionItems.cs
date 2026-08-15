using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed class ProgressionItems
{
    public const string RowStructure = "/Script/fellowship.CRProgressionItemDataRow";
    public const string Segment = "Progression";

    private const string TagField = "ItemID";
    private const string DisplayNameField = "DisplayName";
    private const string DescriptionField = "Description";
    private const string IconField = "IconTexture";
    private const string RarityField = "Rarity";

    private readonly Dictionary<string, NamedRow> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    public int TableCount { get; }

    public ProgressionItems(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, RarityNames rarities)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            TableCount++;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;
                if (PropertyWalk.GetString(row, DisplayNameField) is not { } name) continue;

                var tier = rarities.Tier(PropertyWalk.Get(row, RarityField)?.GenericValue);

                _byTag.TryAdd(
                    tag,
                    new NamedRow(name, PropertyWalk.GetText(row, DescriptionField))
                    {
                        IconPath = PropertyWalk.GetString(row, IconField),
                        Rarity = rarities.Label(tier),
                        RarityTier = tier,
                    });
            }
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so every ItemID.{Segment} tag exports as a bare id with no " +
                "name, no text and no icon.");
        }
    }

    public NamedRow? Find(string tagName) => _byTag.GetValueOrDefault(tagName);
}
