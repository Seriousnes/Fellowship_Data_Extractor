using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One shipped set bonus, as its own tooltip shows it.</summary>
public sealed record SetBonus(string DisplayName, string? Description, int? SetCount)
{
    /// <summary>
    /// The roles the set is offered to, empty where it is offered to all of them. This is where the
    /// per-hero difference lives: Sin Warding ships as three separate rows gated Dps, Healer and
    /// Tank, and Draconic Deceit and Draconic Fury are one set split the same way.
    /// </summary>
    public IReadOnlyList<string>? AllowedRoles { get; init; }

    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    /// <summary>The netId of the augmentation carrying what the set actually does.</summary>
    public int? Augmentation { get; init; }

    public string? IconPath { get; init; }
}

/// <summary>
/// Which set an item counts towards, under the name the game prints.
///
/// A fixed item names its set by the row key, "Quickplay", which is not what a player sees:
/// DT_ItemSetBonuses calls that row Expedition Heroism. A dynamic item does not name a set at all.
/// It names a mapping key - DynamicItemSetBonusMapping.Key.SetBonusB.Proc - and the set it counts
/// towards depends on who is wearing it, because one table per hero maps that key to the row for
/// them. The same helm is Dark Prophecy for every hero, while SetBonusA.Percentage is Draconic
/// Deceit for nine heroes and Draconic Fury for the three the shipped tables file under tanking.
///
/// An exported card is one armour type and one primary stat rather than one hero, so a card that
/// covers several heroes can count towards more than one named set. 32 of the 417 set item cards
/// do, which is why the answer is a list rather than a name.
/// </summary>
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

    /// <summary>The set bonus a "ItemID.SetBonus.X" tag names, or null when no row defines it.</summary>
    public SetBonus? ForTag(string tagName) =>
        tagName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)
            ? _byRow.GetValueOrDefault(tagName[TagPrefix.Length..])
            : null;

    /// <summary>Whether an augmentation id is the mechanics behind one of these rows.</summary>
    public bool PointsAt(int netId) => _augmentations.Contains(netId);

    /// <summary>The set an item's own SetBonusA row key names, as the one-name list a card carries.</summary>
    public IReadOnlyList<string>? ForRow(string? rowName) =>
        rowName is not null && _byRow.TryGetValue(rowName, out var bonus) ? [bonus.DisplayName] : null;

    /// <summary>
    /// The sets a card counts towards: one per hero it covers, deduplicated, in the order the
    /// heroes come. Several heroes reading the same mapping key as the same set is the common case
    /// and collapses to one name.
    /// </summary>
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

    /// <summary>
    /// One of the two gates a set bonus row declares, under the names a reader recognises. These
    /// ship already resolved - EPlayerRole::ROLE_TANK, EPlayerAttribute::ATTRIBUTE_AGILITY - so
    /// there is no ordinal to decode, only the qualifier and the shouting to take off. A row that
    /// names none of a gate is open to everyone and says so by carrying nothing.
    /// </summary>
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
