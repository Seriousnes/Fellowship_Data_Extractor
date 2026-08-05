using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What an item's FixedSlots entries actually are. The item names slots only - "StatCrit",
/// "HeroRelativePrimaryStat" - and the slot definition table says which stat each one carries and
/// which kind of slot it is.
///
/// The magnitude is not here and is not in the build: a dynamic item rolls it from its level at
/// drop time, so an item declares which stats it has, the share of the budget each is worth, and
/// never how much of them. <see cref="StatCalculation"/> holds the rest of that arithmetic.
/// </summary>
public sealed class ItemSlots
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemSlotDefinition";

    private const string StatEnumPrefix = "ECRItemStat::";
    private const string SlotsField = "FixedSlots[";
    private const string WeightsField = "StatWeights";
    private const string TypeField = "SlotType";
    private const string PoolField = "StatPool";

    /// <summary>ECRDynamicItemSlotType's first three values, which are the stat-bearing ones.</summary>
    private const int PrimarySlotType = 0;
    private const int FixedSlotType = 1;
    private const int RandomSlotType = 2;

    private readonly Dictionary<string, string> _statBySlot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _typeBySlot = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _statBySlot.Count;

    /// <summary>The stats a slot rolled at random can land on, which every random slot shares.</summary>
    public IReadOnlyList<string> RandomStatPool { get; private set; } = [];

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
                if (type == RandomSlotType && pool.Length > 0 && RandomStatPool.Count == 0) RandomStatPool = pool;
            }
        }
    }

    /// <summary>
    /// The stats an item's slots carry, in the order the item declares them, each with the share
    /// of the budget its slot is worth.
    ///
    /// There is one shipped weight per stat-bearing slot, so a slot that carries no stat - a gem
    /// socket, a set bonus, a granted ability - is dropped before the two are lined up.
    /// </summary>
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

    /// <summary>The stats a slot's pool names, with the enum prefix taken off.</summary>
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

/// <summary>
/// Data tables are found by the row struct they declare rather than by path, so a table that moves
/// or a second table sharing the struct is still picked up.
/// </summary>
public static class TableFinder
{
    public static IEnumerable<string> ByRowStructure(IReadOnlyList<FAssetData> assets, string rowStructure) =>
        assets
            .Where(a => a.TagsAndValues.Any(kv =>
                kv.Key.Text.Equals("RowStructure", StringComparison.OrdinalIgnoreCase) &&
                kv.Value == rowStructure))
            .Select(a => a.ObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}
