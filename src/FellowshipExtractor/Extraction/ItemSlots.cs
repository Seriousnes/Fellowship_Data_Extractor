using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class ItemSlots
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemSlotDefinition";

    private const string StatEnumPrefix = "ECRItemStat::";
    private const string SlotsField = "FixedSlots[";
    private const string WeightsField = "StatWeights";
    private const string TypeField = "SlotType";
    private const string PoolField = "StatPool";

    private const int PrimarySlotType = 0;
    private const int FixedSlotType = 1;
    private const int RandomSlotType = 2;

    private readonly Dictionary<string, string> _statBySlot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _typeBySlot = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _statBySlot.Count;


    public ItemSlots(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, TypeField)?.GenericValue) is not { } type) continue;

                _typeBySlot.TryAdd(rowName.Text, type);

                var pool = Pool(row);
                if (type == FixedSlotType && pool.Length == 1) _statBySlot.TryAdd(rowName.Text, pool[0]);
            }
        }
    }

    public List<StatSlot> For(UObject asset, StatCalculation calculation)
    {
        var declared = PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith(SlotsField, StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Where(slot => _typeBySlot.TryGetValue(slot, out var type)
                && type is PrimarySlotType or FixedSlotType or RandomSlotType)
            .ToArray();

        if (declared.Length == 0) return [];

        var weights = PropertyWalk.GetString(asset, WeightsField) is { } row
            ? calculation.Weights.GetValueOrDefault(row)
            : null;

        var slots = new List<StatSlot>(declared.Length);

        for (var index = 0; index < declared.Length; index++)
        {
            var slot = declared[index];

            slots.Add(new StatSlot(
                Stat: _statBySlot.GetValueOrDefault(slot, slot),
                Weight: weights is not null && index < weights.Count ? weights[index] : 0)
            {
                Kind = _typeBySlot[slot] switch
                {
                    PrimarySlotType => SlotKinds.Primary,
                    RandomSlotType => SlotKinds.Random,
                    _ => SlotKinds.Fixed,
                },
            });
        }

        return slots;
    }

    private static string[] Pool(IPropertyHolder row)
    {
        if (PropertyWalk.Get(row, PoolField)?.GenericValue is not UScriptArray pool) return [];

        return [.. pool.Properties
            .Select(p => PropertyWalk.AsString(p?.GenericValue))
            .OfType<string>()
            .Select(s => s.StartsWith(StatEnumPrefix, StringComparison.Ordinal) ? s[StatEnumPrefix.Length..] : s)
            .Distinct(StringComparer.Ordinal)];
    }
}

public static class TableFinder
{
    private const string RowStructureKey = "RowStructure";

    public static IEnumerable<string> ByRowStructure(IReadOnlyList<FAssetData> assets, string rowStructure) =>
        assets
            .Where(a => a.TagsAndValues.Any(kv =>
                kv.Key.Text.Equals(RowStructureKey, StringComparison.OrdinalIgnoreCase) &&
                kv.Value == rowStructure))
            .Select(a => a.ObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<(string ObjectPath, string RowStructure)> ByRowStructureAround(
        IReadOnlyList<FAssetData> assets, string prefix, string suffix)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in assets)
        {
            foreach (var (key, value) in asset.TagsAndValues)
            {
                if (!key.Text.Equals(RowStructureKey, StringComparison.OrdinalIgnoreCase)) continue;
                if (value is not { } structure) continue;
                if (!structure.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!structure.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(asset.ObjectPath)) continue;

                yield return (asset.ObjectPath, structure);
            }
        }
    }
}
