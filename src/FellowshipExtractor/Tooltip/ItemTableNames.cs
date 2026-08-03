using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>The text a shipped table holds for one gameplay tag, and the art beside it.</summary>
public sealed record NamedRow(string? DisplayName, string? Description)
{
    /// <summary>
    /// The row's own icon, as an object path. All 31 currency rows carry one, and it is the only
    /// place a currency's art is recorded: the tag reaches no asset to read a texture off.
    /// </summary>
    public string? IconPath { get; init; }
}

/// <summary>
/// The currencies, whose rows are named after a gameplay tag and never reach an asset. A currency
/// is a data table row and nothing else, so a tag naming one resolves to no asset and exports as a
/// bare id however much text the table holds for it.
///
/// A currency row carries no tag at all and is joined by row key, which is the tag's last segment
/// on all but one of the 31 rows. Traits and gems used to be read here too and are not: both grew
/// past a name and a description into <see cref="ItemTraits"/> and <see cref="GemDefinitions"/>,
/// which read the same tables for the rest of what their rows hold.
/// </summary>
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

    /// <summary>The text a tag's own table row holds, or null when no table names it.</summary>
    public NamedRow? Find(string tagName) => _byTag.GetValueOrDefault(tagName);
}
