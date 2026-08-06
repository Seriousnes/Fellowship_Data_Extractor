using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Tooltip;

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

    public IReadOnlyList<(string Hero, string Asset)> FindAll(string mappingKey) =>
        [.. _byKeyAndHero
            .Where(pair => pair.Key.Key.Equals(mappingKey, StringComparison.OrdinalIgnoreCase))
            .Select(pair => (pair.Key.Hero, pair.Value))
            .OrderBy(pair => pair.Hero, StringComparer.OrdinalIgnoreCase)];
}
