using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One of an item's stat slots, resolved to the stats it can hold.</summary>
public sealed record ItemSlot(string Slot)
{
    public string[]? StatPool { get; init; }
    public string[]? LegendaryTraitPool { get; init; }
    public string[]? GemPowerPool { get; init; }

    /// <summary>Slot and stat are byte-valued enums, which cook down to their ordinal.</summary>
    public string? SlotTypeOrdinal { get; init; }
    public string? StatOrdinal { get; init; }
}

/// <summary>
/// What an item's FixedSlots entries actually are. The item names slots only - "StatCrit",
/// "HeroRelativePrimaryStat" - and the slot definition table says which stat each one carries.
/// The magnitude is not here: a dynamic item rolls it from its rarity and level at drop time.
/// </summary>
public sealed class ItemSlots
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemSlotDefinition";

    private readonly Dictionary<string, ItemSlot> _bySlot = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _bySlot.Count;

    public ItemSlots(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                _bySlot.TryAdd(rowName.Text, new ItemSlot(rowName.Text)
                {
                    StatPool = Pool(row, "StatPool"),
                    LegendaryTraitPool = Pool(row, "LegendaryTraitPool"),
                    GemPowerPool = Pool(row, "GemPowerPool"),
                    SlotTypeOrdinal = PropertyWalk.GetString(row, "SlotType"),
                    StatOrdinal = PropertyWalk.GetString(row, "Stat"),
                });
            }
        }
    }

    /// <summary>The slots an item declares, in the order it declares them.</summary>
    public List<ItemSlot> For(UObject asset) =>
        [.. PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith("FixedSlots[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(slot => _bySlot.GetValueOrDefault(slot, new ItemSlot(slot)))];

    private static string[]? Pool(IPropertyHolder row, string name)
    {
        if (PropertyWalk.Get(row, name)?.GenericValue is not UScriptArray array || array.Properties.Count == 0)
        {
            return null;
        }
        return [.. array.Properties.Select(p => PropertyWalk.AsString(p?.GenericValue)).OfType<string>()];
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
