using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed record CustomizationItem(string? DisplayName)
{
    public string? Description { get; init; }

    public string? Acquire { get; init; }

    public string? TitleText { get; init; }

    public string? IconPath { get; init; }

    public string? Rarity { get; init; }

    public int? RarityTier { get; init; }
}

public sealed class CustomizationItems
{
    public const string RowStructurePrefix = "/Script/ProfileCustomizationsRuntime.CRPlayerCustomization";
    public const string RowStructureSuffix = "ItemTableRow";
    public const string Segment = "PlayerCustomization";

    private const string TagField = "ItemId";
    private const string DisplayNameField = "DisplayName";
    private const string TitleTextField = "TitleText";
    private const string DescriptionField = "Description";
    private const string AcquireField = "AcquireDescription";
    private const string IconField = "ItemIcon";
    private const string RarityField = "Rarity";

    private readonly Dictionary<string, CustomizationItem> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<string, string> _nameSourceByKind = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _rowsByKind = new(StringComparer.Ordinal);

    public int Count => _byTag.Count;

    public int TableCount => _rowsByKind.Count;

    public IReadOnlyDictionary<string, int> RowsByKind => _rowsByKind;

    public IReadOnlyDictionary<string, string> NameSourceByKind => _nameSourceByKind;

    public int Unnamed { get; private set; }

    public CustomizationItems(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, RarityNames rarities)
    {
        foreach (var (objectPath, rowStructure) in
            TableFinder.ByRowStructureAround(assets, RowStructurePrefix, RowStructureSuffix))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            var kind = rowStructure[RowStructurePrefix.Length..^RowStructureSuffix.Length];

            var declared = 0;
            var titled = 0;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;

                var displayName = PropertyWalk.GetString(row, DisplayNameField);
                var titleText = PropertyWalk.GetString(row, TitleTextField);
                var tier = rarities.Tier(PropertyWalk.Get(row, RarityField)?.GenericValue);

                if (displayName is not null) declared++;
                else if (titleText is not null) titled++;
                else Unnamed++;

                _rowsByKind[kind] = _rowsByKind.GetValueOrDefault(kind) + 1;

                _byTag.TryAdd(
                    tag,
                    new CustomizationItem(displayName ?? titleText)
                    {
                        Description = PropertyWalk.GetText(row, DescriptionField),
                        Acquire = PropertyWalk.GetText(row, AcquireField),
                        TitleText = titleText,
                        IconPath = PropertyWalk.GetString(row, IconField),
                        Rarity = rarities.Label(tier),
                        RarityTier = tier,
                    });
            }

            _nameSourceByKind[kind] = $"{DisplayNameField} {declared}, {TitleTextField} {titled}";
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares a {RowStructurePrefix}*{RowStructureSuffix} row structure, so every " +
                $"ItemID.{Segment} tag exports as a bare id with no name, no text and no icon.");
        }
    }

    public CustomizationItem? Find(string tagName) => _byTag.GetValueOrDefault(tagName);
}
