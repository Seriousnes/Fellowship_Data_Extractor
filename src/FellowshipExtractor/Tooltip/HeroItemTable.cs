using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

public sealed record HeroItem
{
    public string? DisplayName { get; init; }

    public string? Slot { get; init; }

    public string? ArmorType { get; init; }

    public required IReadOnlyList<StatSlot> Stats { get; init; }

    public string? SetBonus { get; init; }

    public required IReadOnlyList<string> Heroes { get; init; }

    public bool UniqueEquipped { get; init; }

    public string? IconPath { get; init; }
}

public sealed class HeroItemTable
{
    public const string RowStructure = "/Script/fellowship.CRHeroItemDataRow";

    private const string TagPrefix = "ItemID.HeroItem.";
    private const string StatEnumPrefix = "ECRItemStat::";
    private const string StaminaStat = "Stamina";
    private const string NoneValue = "None";

    private readonly Dictionary<string, HeroItem> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _primaryStatByHero = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<StatSlot>> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly AbstractFileProvider _provider;
    private readonly ArmorTypes _armor;
    private readonly ItemSlotNames _slots;
    private readonly VisualCollections _visuals;

    public int Count => _byTag.Count;

    public IReadOnlyDictionary<string, string> PrimaryStatByHero => _primaryStatByHero;

    public HeroItemTable(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ArmorTypes armor,
        ItemSlotNames slots,
        VisualCollections visuals)
    {
        _provider = provider;
        _armor = armor;
        _slots = slots;
        _visuals = visuals;

        var statsByHero = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                var heroes = HeroesOf(row);
                var primary = Template(row, "PrimaryStatsWeightsTemplate", primaryPool: true);

                foreach (var hero in heroes)
                {
                    if (!statsByHero.TryGetValue(hero, out var seen))
                    {
                        statsByHero[hero] = seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }

                    foreach (var slot in primary) seen.Add(slot.Stat);
                }

                _byTag.TryAdd(TagPrefix + rowName.Text, Read(row, heroes, primary));
            }
        }

        ResolvePrimaryStats(statsByHero);
    }

    public HeroItem? Find(string tagName) => _byTag.GetValueOrDefault(tagName);

    private HeroItem Read(FStructFallback row, IReadOnlyList<string> heroes, IReadOnlyList<StatSlot> primary)
    {
        var slot = Slot(row);

        return new HeroItem
        {
            DisplayName = PropertyWalk.GetString(row, "DisplayName"),
            Slot = slot?.Shipped,
            ArmorType = _armor.Label(PropertyWalk.Get(row, "ArmorType")?.GenericValue),
            Stats = [.. primary, .. Template(row, "SecondaryStatsWeightsTemplate", primaryPool: false)],
            SetBonus = Named(PropertyWalk.GetString(row, "SetBonusA")),
            Heroes = heroes,
            UniqueEquipped = PropertyWalk.Get(row, "IsUniqueEquippable")?.GenericValue is true,
            IconPath = Icon(row, slot?.Type),
        };
    }

    private static List<string> HeroesOf(FStructFallback row)
    {
        var heroes = new List<string>();

        foreach (var tag in PropertyWalk.GetTags(row, "ItemExclusiveForHeroes") ?? [])
        {
            Add(tag);
        }

        if (PropertyWalk.Get(row, "DefaultItemForHeroes")?.GenericValue is UScriptMap defaults)
        {
            foreach (var (key, _) in defaults.Properties) Add(PropertyWalk.AsString(key?.GenericValue));
        }

        return heroes;

        void Add(string? tag)
        {
            if (tag is null) return;
            if (!tag.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase)) return;

            var codename = HeroNames.LastSegment(tag);
            if (!heroes.Contains(codename, StringComparer.OrdinalIgnoreCase)) heroes.Add(codename);
        }
    }

    private IReadOnlyList<StatSlot> Template(FStructFallback row, string field, bool primaryPool)
    {
        if (PropertyWalk.GetString(row, field) is not { } path) return [];
        if (_templateCache.TryGetValue(path, out var cached)) return cached;

        var stats = new List<StatSlot>();

        if (_provider.TryLoadPackageObject<UObject>(AbilityPresentationIndex.PackagePath(path), out var template) &&
            PropertyWalk.Get(template, "StatsWeights")?.GenericValue is UScriptMap weights)
        {
            foreach (var (key, value) in weights.Properties)
            {
                if (PropertyWalk.AsString(key?.GenericValue) is not { } stat) continue;

                var name = stat.StartsWith(StatEnumPrefix, StringComparison.Ordinal)
                    ? stat[StatEnumPrefix.Length..]
                    : stat;

                stats.Add(new StatSlot(name, (int)(PropertyWalk.AsNumber(value?.GenericValue) ?? 0))
                {
                    Kind = primaryPool && !name.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase)
                        ? SlotKinds.Primary
                        : SlotKinds.Fixed,
                });
            }
        }

        return _templateCache[path] = stats;
    }

    private SlotName? Slot(FStructFallback row) =>
        PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "HeroItemType")?.GenericValue) is { } ordinal
            ? _slots.ByOrdinal(ordinal)
            : null;

    private string? Icon(FStructFallback row, string? slot)
    {
        var texture = PropertyWalk.GetString(row, "ItemIconTexture");

        if (slot is null) return texture;
        if (PropertyWalk.GetString(row, "VisualCollection") is not { } collectionPath) return texture;

        var variation = (int)(PropertyWalk.AsNumber(
            PropertyWalk.Get(row, "VisualCollectionSkinVariationIndex")?.GenericValue) ?? 0);

        return _visuals.Icon(collectionPath, variation, slot) ?? texture;
    }

    private void ResolvePrimaryStats(Dictionary<string, HashSet<string>> statsByHero)
    {
        if (statsByHero.Count == 0) return;

        var shared = new HashSet<string>(statsByHero.Values.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var stats in statsByHero.Values) shared.IntersectWith(stats);

        foreach (var (hero, stats) in statsByHero)
        {
            var own = stats.Where(stat => !shared.Contains(stat)).ToArray();
            if (own.Length == 1) _primaryStatByHero[hero] = own[0];
        }
    }

    private static string? Named(string? value) =>
        string.IsNullOrEmpty(value) || value.Equals(NoneValue, StringComparison.Ordinal) ? null : value;
}
