using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record SetBonus(string DisplayName, string? Description, int? SetCount)
{
    public IReadOnlyList<string>? AllowedRoles { get; init; }

    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    public int? Augmentation { get; init; }

    public string? IconPath { get; init; }
}

public sealed class ItemSetBonuses
{
    public const string RowStructure = "/Script/fellowship.CRHeroItemSetBonusRow";
    public const string MappingRowStructure = "/Script/fellowship.CRDynamicItemSetBonusMapping";

    private const string TagPrefix = "ItemID.SetBonus.";
    private const string ItemTable = "ItemID";
    private const string DisplayNameField = "DisplayName";
    private const string DescriptionField = "Description";
    private const string SetCountField = "SetCount";
    private const string MappingKeyField = "MappingKey";
    private const string HeroIdField = "HeroID";
    private const string SetBonusIdField = "SetBonusID";
    private const string RolesField = "AllowedRoles";
    private const string AttributesField = "AllowedAttributes";
    private const string GrantedField = "GrantedAbilities";
    private const string IconField = "SetBonusIcon";

    private readonly Dictionary<string, SetBonus> _byRow = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _byMappingKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _augmentations = [];
    private readonly Dictionary<int, SetBonus> _byAugmentation = [];
    private readonly Dictionary<string, int> _netIds = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byRow.Count;

    public ItemSetBonuses(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        AugmentationIds augmentations,
        IReadOnlyList<TagRecord> tags)
    {
        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(ItemTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (!tag.Name.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            _netIds.TryAdd(tag.Name[TagPrefix.Length..], tag.NetId);
        }

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, DisplayNameField) is not { } name) continue;

                var augmentationId = augmentations.For(PropertyWalk.GetString(row, GrantedField));

                if (!_byRow.TryAdd(rowName.Text, new SetBonus(
                    name,
                    PropertyWalk.GetText(row, DescriptionField),
                    (int?)PropertyWalk.AsNumber(PropertyWalk.Get(row, SetCountField)?.GenericValue))
                {
                    AllowedRoles = Gate(row, RolesField),
                    AllowedAttributes = Gate(row, AttributesField),
                    Augmentation = augmentationId,
                    IconPath = PropertyWalk.GetString(row, IconField),
                })) continue;

                if (augmentationId is not { } pointed) continue;

                _augmentations.Add(pointed);
                _byAugmentation.TryAdd(pointed, _byRow[rowName.Text]);
            }
        }

        foreach (var objectPath in TableFinder.ByRowStructure(assets, MappingRowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, MappingKeyField) is not { } key) continue;
                if (PropertyWalk.GetString(row, HeroIdField) is not { } hero) continue;
                if (PropertyWalk.GetString(row, SetBonusIdField) is not { } setBonus) continue;

                if (!_byMappingKey.TryGetValue(key, out var byHero))
                {
                    _byMappingKey[key] = byHero = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                byHero[HeroNames.LastSegment(hero)] = RowKey(setBonus);
            }
        }
    }

    public SetBonus? ForTag(string tagName) =>
        tagName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)
            ? _byRow.GetValueOrDefault(tagName[TagPrefix.Length..])
            : null;

    public SetBonus? ForAugmentation(int netId) => _byAugmentation.GetValueOrDefault(netId);

    public bool PointsAt(int netId) => _augmentations.Contains(netId);

    public IReadOnlyList<IconSource>? ForRow(string? rowName) =>
        rowName is not null && Source(rowName) is { } source ? [source] : null;

    public IReadOnlyList<IconSource>? ForHeroes(string? mappingKey, IEnumerable<string> heroCodenames)
    {
        if (mappingKey is null) return null;
        if (!_byMappingKey.TryGetValue(mappingKey, out var byHero)) return null;

        var named = new List<IconSource>();

        foreach (var codename in heroCodenames)
        {
            if (!byHero.TryGetValue(codename, out var rowName)) continue;
            if (Source(rowName) is not { } source) continue;
            if (!named.Contains(source)) named.Add(source);
        }

        return named.Count == 0 ? null : named;
    }

    private IconSource? Source(string rowName) =>
        _byRow.TryGetValue(rowName, out var bonus) && _netIds.TryGetValue(rowName, out var netId)
            ? new IconSource(ItemTable, netId, bonus.DisplayName)
            : null;

    private static IReadOnlyList<string>? Gate(IPropertyHolder row, string field)
    {
        if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray values) return null;

        var named = values.Properties
            .Select(p => PropertyWalk.AsString(p?.GenericValue))
            .OfType<string>()
            .Select(Titled)
            .ToArray();

        return named.Length == 0 ? null : named;
    }

    private static string Titled(string value)
    {
        var leaf = value[(value.LastIndexOf('_') + 1)..];
        return leaf.Length == 0 ? value : char.ToUpperInvariant(leaf[0]) + leaf[1..].ToLowerInvariant();
    }

    private static string RowKey(string tagName) =>
        tagName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)
            ? tagName[TagPrefix.Length..]
            : tagName;
}
