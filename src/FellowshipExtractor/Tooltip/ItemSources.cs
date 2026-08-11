using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tooltip;

public sealed record DroptableGrant(string Droptable, SourceKind Kind, string Name, int ItemLevel)
{
    public required IReadOnlyList<string> Tags { get; init; }
}

public sealed class ItemSources
{
    private const string DroptableClass = "CRSimpleDroptableDefinition";
    private const string GroupMarker = "Group";
    private const string ItemLevelField = "ItemLevel";
    private const string ContainsTagsField = "ContainsTags";
    private const string DisplayNameField = "DisplayName";
    private const string LegendaryRow = "Legendary";
    private const string AnyDungeon = "Any dungeon";
    private const string StartingGear = "Starting gear";

    private readonly DungeonNames _dungeons;
    private readonly HeroItemTable _heroItems;
    private readonly DropItemLevels _drops;
    private readonly HeroNames _heroes;

    private readonly List<DroptableGrant> _grants = [];
    private readonly Dictionary<string, DroptableGrant> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<DroptableGrant> Grants => _grants;

    public int Count => _grants.Count;

    public int TieredRows => _byTag.Count;

    public ItemSources(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        DungeonNames dungeons,
        VendorInventories vendors,
        HeroItemTable heroItems,
        DropItemLevels drops,
        HeroNames heroes)
    {
        _dungeons = dungeons;
        _heroItems = heroItems;
        _drops = drops;
        _heroes = heroes;

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(DroptableClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (!provider.TryLoadPackage(
                AbilityPresentationIndex.PackagePath(asset.ObjectPath), out var package)) continue;

            var exports = package.GetExports().ToArray();
            var named = asset.AssetName.Text;

            var root = exports.FirstOrDefault(export => export.Name.Equals(named, StringComparison.Ordinal));

            var vendor = vendors.VendorOf(named);
            var printed = root is null ? null : PropertyWalk.GetText(root, DisplayNameField);

            var kind = vendor is not null ? SourceKind.Vendor : SourceKind.Starmap;
            var title = vendor ?? printed;

            if (title is not { Length: > 0 }) continue;

            foreach (var export in exports)
            {
                if (!export.ExportType.Contains(GroupMarker, StringComparison.Ordinal)) continue;
                if (PropertyWalk.AsOrdinal(PropertyWalk.Get(export, ItemLevelField)?.GenericValue)
                    is not { } level) continue;

                _grants.Add(new DroptableGrant(named, kind, title, level) { Tags = Tags(export) });
            }
        }

        foreach (var grant in _grants)
        {
            foreach (var tag in grant.Tags) _byTag.TryAdd(tag, grant);
        }
    }

    private static IReadOnlyList<string> Tags(UObject group) =>
        PropertyWalk.Get(group, ContainsTagsField)?.GenericValue is UScriptArray listed
            ? [.. listed.Properties.Select(entry => PropertyWalk.AsString(entry?.GenericValue)).OfType<string>()]
            : [];

    public DroptableGrant? Granting(string droptable) =>
        _grants.FirstOrDefault(grant => grant.Droptable.Equals(droptable, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<ItemSource> Fixed(string tagName, out int? itemLevel)
    {
        itemLevel = null;

        if (_heroItems.Find(tagName) is not { } row) return [];

        var sources = new List<ItemSource>();

        if (row.DefaultItemLevel is { } outfit)
        {
            itemLevel = outfit;

            foreach (var codename in row.Heroes)
            {
                sources.Add(new ItemSource(SourceKind.Character, $"{StartingGear} - {Shipped(codename)}"));
            }

            if (sources.Count == 0) sources.Add(new ItemSource(SourceKind.Character, StartingGear));
        }

        foreach (var tag in row.Tags)
        {
            if (!_byTag.TryGetValue(tag, out var grant)) continue;

            itemLevel ??= grant.ItemLevel;

            if (!sources.Any(source => source.Kind == grant.Kind && source.Name == grant.Name))
            {
                sources.Add(new ItemSource(grant.Kind, grant.Name));
            }
        }

        return sources;
    }

    public IReadOnlyList<ItemSource> Dropped(IReadOnlyList<string> dungeonTags, Func<string, int?> netIds)
    {
        if (dungeonTags.Count == 0) return [new ItemSource(SourceKind.Dungeon, AnyDungeon)];

        var sources = new List<ItemSource>(dungeonTags.Count);

        foreach (var tag in dungeonTags)
        {
            if (_dungeons.Find(tag) is not { } dungeon) continue;

            sources.Add(new ItemSource(SourceKind.Dungeon, dungeon.DisplayName)
            {
                Dungeon = netIds(tag) is { } netId
                    ? new EntityReference(EntityType.Dungeon, netId, dungeon.DisplayName)
                    : null,
            });
        }

        return sources;
    }

    public int? DropLevel(bool legendary) =>
        _drops.Plateau(legendary ? LegendaryRow : DropItemLevels.DefaultRow);

    private string Shipped(string codename) => _heroes.Shipped(codename) ?? codename;
}
