using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One rarity an item exists at and the art it shows there.</summary>
public readonly record struct RarityIcon(string Rarity, string IconPath);

/// <summary>
/// Which art a dynamic hero item shows. A dynamic definition carries no icon of its own for the
/// eight slots a collection dresses; it names a visual collection mapping key, and the twelve
/// shipped mapping tables turn that key and the wearer's hero into one CRVisualCollection. The
/// collection then holds one rung per art tier and the item picks a rung.
///
/// The rung is the item's rarity, banded. The shipped client is the only statement of the banding:
/// UCRHeroItem::GetIcon reads the item instance's ItemRarity, passes it through a jump table that
/// answers 0 for Common and Uncommon, 1 for Rare and Epic and 2 for Champion, Heroic and Legendary,
/// then clamps the answer to the last rung the collection actually ships. Nothing cooked states it,
/// so <see cref="Rung"/> is the one fact here that is written down rather than read; it was
/// measured at 0x145300FC0 with the band table at 0x14530281C in
/// fellowship-Win64-Shipping.exe 0.4.2.0, and the clamp is what makes the eleven legendary
/// definitions - whose collections ship a single rung - resolve at all.
///
/// The same function answers for the fixed hero items, which reach it through the same class:
/// CRDynamicHeroItemDefinition derives from CRHeroItem and inherits the icon getter unchanged,
/// overriding only how the collection is found.
/// </summary>
public sealed class DynamicItemVisuals
{
    public const string RowStructure = "/Script/fellowship.CRDynamicItemVisualCollectionMapping";

    private const string MappingKeyField = "MappingKey";
    private const string HeroField = "HeroID";
    private const string CollectionField = "VisualCollection";
    private const string AllRaritiesField = "DropsInAllRarities";
    private const string SpecificRaritiesField = "DropsInSpecificRarities";

    /// <summary>
    /// The rung each rarity tier shows, lowest tier first. Read out of the shipping client rather
    /// than out of the build's data, which says nothing about it anywhere.
    /// </summary>
    private static readonly int[] RungByTier = [0, 0, 1, 1, 2, 2, 2];

    private readonly Dictionary<string, string> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly VisualCollections _visuals;
    private readonly RarityNames _rarities;

    /// <summary>How many hero-and-key pairs the mapping tables answer for.</summary>
    public int Count => _collections.Count;

    public DynamicItemVisuals(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        VisualCollections visuals,
        RarityNames rarities)
    {
        _visuals = visuals;
        _rarities = rarities;

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, MappingKeyField) is not { } key) continue;
                if (PropertyWalk.GetString(row, HeroField) is not { } hero) continue;
                if (PropertyWalk.GetString(row, CollectionField) is not { } collection) continue;

                _collections.TryAdd(Key(key, HeroNames.LastSegment(hero)), collection);
            }
        }
    }

    /// <summary>
    /// The art one hero sees an item as, one entry per rarity the item drops at, in ladder order.
    /// Empty where the item's slot has no icon on a collection at all - neck, wrists, ring and
    /// relic have none - which is exactly where the definition carries a DefaultIcon instead.
    /// </summary>
    public IReadOnlyList<RarityIcon> Icons(
        string? mappingKey, string heroCodename, string? slotType, IReadOnlyList<int> tiers)
    {
        if (mappingKey is null || slotType is null) return [];
        if (!_collections.TryGetValue(Key(mappingKey, heroCodename), out var collection)) return [];

        var rungs = _visuals.RungCount(collection);
        if (rungs == 0) return [];

        var icons = new List<RarityIcon>();

        foreach (var tier in tiers)
        {
            if (_rarities.Label(tier) is not { } rarity) continue;
            if (Rung(tier, rungs) is not { } rung) continue;
            if (_visuals.Icon(collection, rung, slotType) is not { } path) continue;

            icons.Add(new RarityIcon(rarity, path));
        }

        return icons;
    }

    /// <summary>
    /// The rarities an item exists at, in ladder order. Every definition states them: 226 of the
    /// 237 list Uncommon through Heroic and the eleven legendaries list Legendary alone, so the
    /// icons an item can show are bounded by what it drops as rather than by the whole ladder.
    /// </summary>
    public IReadOnlyList<int> Tiers(IPropertyHolder definition)
    {
        if (PropertyWalk.Get(definition, AllRaritiesField)?.GenericValue is true) return [.. _rarities.Tiers];

        if (PropertyWalk.Get(definition, SpecificRaritiesField)?.GenericValue is not UScriptArray listed) return [];

        return
        [
            .. listed.Properties
                .Select(entry => _rarities.Tier(entry?.GenericValue))
                .OfType<int>()
                .Distinct()
                .Order(),
        ];
    }

    /// <summary>
    /// The rung a rarity tier shows in a collection that ships this many, or null for a tier off
    /// the end of the banding. The clamp is the client's: a collection with one rung shows it at
    /// every rarity.
    /// </summary>
    private static int? Rung(int tier, int rungs) =>
        tier < 0 || tier >= RungByTier.Length ? null : Math.Min(RungByTier[tier], rungs - 1);

    private static string Key(string mappingKey, string heroCodename) => $"{mappingKey}#{heroCodename}";
}
