using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record AbilityRankRow(int NetId, int MaxRank, IReadOnlyList<string> Slots);

public sealed class DynamicAbilityRanks
{
    public const string RowStructure = "/Script/fellowship.CRDynamicItemAbilityRankDefinition";
    public const string Table = "DynamicItemAbilityRank";

    private const string IdField = "ID";
    private const string MaxRankField = "MaxRank";
    private const string PoolsField = "PoolIds";
    private const string GrantsField = "GrantedAbilitiesAsset";
    private const string PoolTagPrefix = "DynamicItemAbilityPool.";

    private readonly Dictionary<int, AbilityRankRow> _byGlyph = [];

    public int Count => _byGlyph.Count;

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

        var rows = 0;

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                rows++;

                var rowTag = PropertyWalk.GetString(row, IdField)
                    ?? throw new InvalidOperationException($"{rowName.Text} in {objectPath} states no {IdField}.");

                if (!netIdByTag.TryGetValue(rowTag, out var rankNetId))
                {
                    throw new InvalidOperationException(
                        $"{rowName.Text} in {objectPath} is {rowTag}, and the {Table} tag table has no id for it.");
                }

                var granted = PropertyWalk.GetString(row, GrantsField);

                if (augmentations.For(granted) is not { } glyph)
                {
                    throw new InvalidOperationException(
                        $"{rowTag} grants {granted ?? "nothing"}, which no augmentation tag names.");
                }

                var maxRank = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MaxRankField)?.GenericValue)
                    ?? throw new InvalidOperationException($"{rowTag} states no {MaxRankField}.");

                var slots = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pool in Tags(row, PoolsField))
                {
                    var named = pool.StartsWith(PoolTagPrefix, StringComparison.OrdinalIgnoreCase)
                        ? slotNames.Find(pool[PoolTagPrefix.Length..])
                        : null;

                    if (named is null)
                    {
                        throw new InvalidOperationException(
                            $"{rowTag} rolls on {pool}, and no item slot name covers it.");
                    }

                    slots.Add(named.Shipped);
                }

                if (!_byGlyph.TryAdd(glyph, new AbilityRankRow(rankNetId, maxRank, [.. slots])))
                {
                    throw new InvalidOperationException(
                        $"{rowTag} and {_byGlyph[glyph].NetId} both grant augmentation {glyph}.");
                }
            }
        }

        if (_byGlyph.Count == 0)
        {
            throw new InvalidOperationException(
                $"{rows} rows of {RowStructure} name a glyph, so no glyph states the ranks it runs to, the " +
                "slots it rolls on, or the id FellowshipLogs records it under.");
        }
    }

    public AbilityRankRow? For(int glyphNetId) => _byGlyph.GetValueOrDefault(glyphNetId);

    private static IEnumerable<string> Tags(IPropertyHolder row, string field)
    {
        if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (PropertyWalk.AsString(element?.GenericValue) is { } tag) yield return tag;
        }
    }
}
