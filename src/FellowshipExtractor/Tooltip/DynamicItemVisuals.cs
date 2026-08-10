using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public readonly record struct RarityIcon(string Rarity, string IconPath);

public sealed class DynamicItemVisuals
{
    public const string RowStructure = "/Script/fellowship.CRDynamicItemVisualCollectionMapping";

    private const string MappingKeyField = "MappingKey";
    private const string HeroField = "HeroID";
    private const string CollectionField = "VisualCollection";
    private const string AllRaritiesField = "DropsInAllRarities";
    private const string SpecificRaritiesField = "DropsInSpecificRarities";

    private static readonly int[] RungByTier = [0, 0, 1, 1, 2, 2, 2];

    private readonly Dictionary<string, string> _collections = new(StringComparer.OrdinalIgnoreCase);
    private readonly VisualCollections _visuals;
    private readonly RarityNames _rarities;

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

    public IReadOnlyList<RarityIcon> Icons(string? mappingKey, string heroCodename, string? slotType)
    {
        if (mappingKey is null || slotType is null) return [];
        if (!_collections.TryGetValue(Key(mappingKey, heroCodename), out var collection)) return [];

        var rungs = _visuals.RungCount(collection);
        if (rungs == 0) return [];

        var icons = new List<RarityIcon>();

        foreach (var tier in _rarities.Tiers)
        {
            if (_rarities.Label(tier) is not { } rarity) continue;
            if (Rung(tier, rungs) is not { } rung) continue;
            if (_visuals.Icon(collection, rung, slotType) is not { } path) continue;

            icons.Add(new RarityIcon(rarity, path));
        }

        return icons;
    }

    public IReadOnlyList<string> Rarities(IPropertyHolder definition) =>
        [.. Tiers(definition).Select(tier => _rarities.Label(tier)).OfType<string>()];

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

    private static int? Rung(int tier, int rungs) =>
        tier < 0 || tier >= RungByTier.Length ? null : Math.Min(RungByTier[tier], rungs - 1);

    private static string Key(string mappingKey, string heroCodename) => $"{mappingKey}#{heroCodename}";
}
