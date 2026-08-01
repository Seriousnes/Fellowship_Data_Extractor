using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What an item's FixedSlots entries actually are. The item names slots only - "StatCrit",
/// "HeroRelativePrimaryStat" - and the slot definition table says which stat each one carries.
///
/// The magnitude is not here and is not in the build: a dynamic item rolls it from its rarity and
/// its level at drop time, so an item declares which stats it has and never how much of them.
/// </summary>
public sealed class ItemSlots
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemSlotDefinition";
    private const string StatEnumPrefix = "ECRItemStat::";

    private readonly Dictionary<string, string> _statBySlot = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _statBySlot.Count;

    public ItemSlots(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (Stat(row) is { } stat) _statBySlot.TryAdd(rowName.Text, stat);
            }
        }
    }

    /// <summary>The stats an item's slots carry, in the order the item declares them.</summary>
    public List<string> For(UObject asset) =>
        [.. PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith("FixedSlots[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(slot => _statBySlot.GetValueOrDefault(slot, slot))];

    /// <summary>
    /// The one stat a fixed slot holds. Every slot that names a pool names exactly one stat in it;
    /// the slot whose stat depends on the hero names none, and its own name says so.
    /// </summary>
    private static string? Stat(IPropertyHolder row)
    {
        if (PropertyWalk.Get(row, "StatPool")?.GenericValue is not UScriptArray pool) return null;

        var stats = pool.Properties
            .Select(p => PropertyWalk.AsString(p?.GenericValue))
            .OfType<string>()
            .Select(s => s.StartsWith(StatEnumPrefix, StringComparison.Ordinal) ? s[StatEnumPrefix.Length..] : s)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return stats.Length == 1 ? stats[0] : null;
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
