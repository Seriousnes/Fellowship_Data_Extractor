using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The ability a dynamic item grants, which depends on both the item and the hero holding it.
///
/// An item names only a mapping key. The rows that turn that key into an actual ability live in
/// per-hero tables, each row carrying the key it answers and the hero it answers for, so the
/// lookup is on the pair rather than on either alone.
/// </summary>
public sealed class GrantedAbilityMappings
{
    public const string RowStructure = "/Script/fellowship.CRDynamicItemGrantedAbilityMapping";

    private readonly Dictionary<(string Key, string Hero), string> _byKeyAndHero = new();

    public int TableCount { get; }
    public int RowCount { get; }

    public GrantedAbilityMappings(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var tables = assets
            .Where(a => a.TagsAndValues.Any(kv =>
                kv.Key.Text.Equals("RowStructure", StringComparison.OrdinalIgnoreCase) &&
                kv.Value == RowStructure))
            .Select(a => a.ObjectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var objectPath in tables)
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            TableCount++;

            foreach (var (_, row) in table.RowMap)
            {
                var key = PropertyWalk.GetString(row, "MappingKey");
                var hero = PropertyWalk.GetString(row, "HeroID");
                var ability = PropertyWalk.GetString(row, "GrantedAbilityAsset");
                if (key is null || hero is null || ability is null) continue;

                RowCount++;
                _byKeyAndHero.TryAdd((key, HeroNames.LastSegment(hero)), ability);
            }
        }
    }

    /// <summary>The ability asset for a mapping key on a given hero, or null if there is none.</summary>
    public string? Find(string mappingKey, string heroCodename)
    {
        foreach (var ((key, hero), ability) in _byKeyAndHero)
        {
            if (key.Equals(mappingKey, StringComparison.OrdinalIgnoreCase)
                && hero.Equals(heroCodename, StringComparison.OrdinalIgnoreCase))
            {
                return ability;
            }
        }
        return null;
    }

    /// <summary>
    /// Every hero the mapping key answers for, with the ability each one gets. An item that names
    /// a key but no heroes still grants something; the tables are the only record of what, and a
    /// relic's key answers the same ability for all twelve while a legendary's answers a different
    /// one per hero, so the caller decides from the shape rather than from the item.
    /// </summary>
    public IReadOnlyList<(string Hero, string Asset)> FindAll(string mappingKey) =>
        [.. _byKeyAndHero
            .Where(pair => pair.Key.Key.Equals(mappingKey, StringComparison.OrdinalIgnoreCase))
            .Select(pair => (pair.Key.Hero, pair.Value))
            .OrderBy(pair => pair.Hero, StringComparer.OrdinalIgnoreCase)];
}
