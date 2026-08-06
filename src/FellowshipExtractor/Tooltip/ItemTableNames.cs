using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

public sealed record NamedRow(string? DisplayName, string? Description)
{
    public string? IconPath { get; init; }
}

public sealed class ItemTableNames
{
    public const string CurrencyRowStructure = "/Script/fellowship.CRCurrencyItemDataRow";

    private const string CurrencyTagPrefix = "ItemID.Currency.";
    private const string DisplayNameField = "DisplayName";
    private const string DescriptionField = "Description";
    private const string IconField = "IconTexture";

    private readonly Dictionary<string, NamedRow> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    public ItemTableNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, CurrencyRowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, DisplayNameField) is not { } name) continue;

                _byTag.TryAdd(
                    CurrencyTagPrefix + rowName.Text,
                    new NamedRow(name, PropertyWalk.GetText(row, DescriptionField))
                    {
                        IconPath = PropertyWalk.GetString(row, IconField),
                    });
            }
        }
    }

    public NamedRow? Find(string tagName) => _byTag.GetValueOrDefault(tagName);
}
