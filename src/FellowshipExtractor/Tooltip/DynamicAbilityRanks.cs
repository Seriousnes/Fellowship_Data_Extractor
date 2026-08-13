using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record AbilityRankRow(
    int MaxRank,
    IReadOnlyList<string> Slots,
    IReadOnlyDictionary<string, int> NetIdByHero);

public sealed class DynamicAbilityRanks
{
    public const string RowStructure = "/Script/fellowship.CRDynamicItemAbilityRankDefinition";
    public const string Table = "DynamicItemAbilityRank";

    private const string IdField = "ID";
    private const string HeroField = "HeroID";
    private const string MaxRankField = "MaxRank";
    private const string PoolsField = "PoolIds";
    private const string GrantsField = "GrantedAbilitiesAsset";
    private const string HeroTagPrefix = "CharacterID.Hero.";
    private const string PoolTagPrefix = "DynamicItemAbilityPool.";

    private readonly Dictionary<int, AbilityRankRow> _byBlessing = [];

    public int Count => _byBlessing.Count;

    public DynamicAbilityRanks(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyList<TagRecord> tags,
        AugmentationIds augmentations,
        ItemSlotNames slotNames)
    {
        var netIdByTag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (tag.Table.Equals(Table, StringComparison.OrdinalIgnoreCase)) netIdByTag.TryAdd(tag.Name, tag.NetId);
        }

        var ranks = new Dictionary<int, int>();
        var slots = new Dictionary<int, SortedSet<string>>();
        var heroes = new Dictionary<int, Dictionary<string, int>>();

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (Tag(row, IdField) is not { } rowTag) continue;
                if (!netIdByTag.TryGetValue(rowTag, out var rankNetId)) continue;
                if (augmentations.For(PropertyWalk.GetString(row, GrantsField)) is not { } blessing) continue;
                if (Tag(row, HeroField) is not { } heroTag) continue;
                if (!heroTag.StartsWith(HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var codename = heroTag[HeroTagPrefix.Length..];

                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MaxRankField)?.GenericValue) is { } max)
                {
                    ranks[blessing] = Math.Max(ranks.GetValueOrDefault(blessing), max);
                }

                if (!slots.TryGetValue(blessing, out var worn))
                {
                    slots[blessing] = worn = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var pool in Tags(row, PoolsField))
                {
                    if (!pool.StartsWith(PoolTagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (slotNames.Named(pool[PoolTagPrefix.Length..]) is { } named) worn.Add(named.Shipped);
                }

                if (!heroes.TryGetValue(blessing, out var byHero))
                {
                    heroes[blessing] = byHero = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                byHero[codename] = rankNetId;
            }
        }

        foreach (var (blessing, byHero) in heroes)
        {
            _byBlessing[blessing] = new AbilityRankRow(
                ranks.GetValueOrDefault(blessing),
                [.. slots.GetValueOrDefault(blessing) ?? []],
                byHero);
        }

        if (_byBlessing.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so no blessing states the ranks it runs to, the slots it " +
                "rolls on, or the per hero id FellowshipLogs records it under.");
        }
    }

    public AbilityRankRow? For(int blessingNetId) => _byBlessing.GetValueOrDefault(blessingNetId);

    private static string? Tag(IPropertyHolder row, string field) => PropertyWalk.GetString(row, field);

    private static IEnumerable<string> Tags(IPropertyHolder row, string field)
    {
        if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (PropertyWalk.AsString(element?.GenericValue) is { } tag) yield return tag;
        }
    }
}
