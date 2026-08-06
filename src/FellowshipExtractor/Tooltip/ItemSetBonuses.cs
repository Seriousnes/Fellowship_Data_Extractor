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

    public int Count => _byRow.Count;

    public ItemSetBonuses(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, AugmentationIds augmentations)
    {
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

                if (augmentationId is not null) _augmentations.Add(augmentationId.Value);
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

    public bool PointsAt(int netId) => _augmentations.Contains(netId);

    public IReadOnlyList<string>? ForRow(string? rowName) =>
        rowName is not null && _byRow.TryGetValue(rowName, out var bonus) ? [bonus.DisplayName] : null;

    public IReadOnlyList<string>? ForHeroes(string? mappingKey, IEnumerable<string> heroCodenames)
    {
        if (mappingKey is null) return null;
        if (!_byMappingKey.TryGetValue(mappingKey, out var byHero)) return null;

        var names = new List<string>();

        foreach (var codename in heroCodenames)
        {
            if (!byHero.TryGetValue(codename, out var rowName)) continue;
            if (!_byRow.TryGetValue(rowName, out var bonus)) continue;
            if (!names.Contains(bonus.DisplayName, StringComparer.Ordinal)) names.Add(bonus.DisplayName);
        }

        return names.Count == 0 ? null : names;
    }

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
