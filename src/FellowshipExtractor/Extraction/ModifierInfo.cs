using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed record Modifier(string Tag)
{
    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public IPropertyHolder? Row { get; init; }

    public string? IconPath { get; init; }

    public int? MinimumHeroRank { get; init; }
}

public sealed class ModifierInfo
{
    public const string RowStructure = "/Script/fellowship.CRModifierInfoDataRow";
    public const string Table = "Modifiers";

    private const string TagField = "ModifierID";
    private const string DisplayNameField = "DisplayName";
    private const string DescriptionField = "Description";
    private const string DynamicDisplayNameField = "DynamicDisplayName";
    private const string DynamicDescriptionField = "DynamicDescription";
    private const string DynamicTextField = "Text";
    private const string IconField = "UIIcon";
    private const string MinimumHeroRankField = "MinimumHeroRankRequired";

    private readonly Dictionary<string, Modifier> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    public int TableCount { get; }

    public int WithIcon => _byTag.Values.Count(modifier => modifier.IconPath is not null);

    public ModifierInfo(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            TableCount++;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;

                _byTag[tag] = new Modifier(tag)
                {
                    DisplayName = Written(row, DisplayNameField) ?? Dynamic(row, DynamicDisplayNameField),
                    Description = Written(row, DescriptionField) ?? Dynamic(row, DynamicDescriptionField),
                    Row = row,
                    IconPath = PropertyWalk.GetString(row, IconField),
                    MinimumHeroRank = PropertyWalk.AsOrdinal(
                        PropertyWalk.Get(row, MinimumHeroRankField)?.GenericValue),
                };
            }
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so every {Table} tag exports as a bare id with no name, no " +
                "text and no icon.");
        }
    }

    public Modifier? ForTag(string tagName) => _byTag.GetValueOrDefault(tagName);

    private static string? Written(IPropertyHolder row, string field) =>
        PropertyWalk.GetText(row, field) is { Length: > 0 } text ? text : null;

    private static string? Dynamic(IPropertyHolder row, string field) =>
        PropertyWalk.GetStruct(row, field) is { } dynamic &&
        PropertyWalk.GetString(dynamic, DynamicTextField) is { Length: > 0 } text
            ? text
            : null;
}
