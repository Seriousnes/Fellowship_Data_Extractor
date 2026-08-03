using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One row of the fixed hero item table, reduced to what a tooltip shows.</summary>
public sealed record HeroItem
{
    public string? DisplayName { get; init; }

    /// <summary>The slot the item goes in: Head, Shoulders, Ring.</summary>
    public string? Slot { get; init; }

    public string? ArmorType { get; init; }

    /// <summary>
    /// The stats the item carries, primary first, named as the item's own stat pools are, each
    /// with the share of its pool the row's weight template gives it.
    /// </summary>
    public required IReadOnlyList<StatSlot> Stats { get; init; }

    /// <summary>The set the item belongs to, on the 90 rows that belong to one.</summary>
    public string? SetBonus { get; init; }

    /// <summary>The hero codenames the item is for.</summary>
    public required IReadOnlyList<string> Heroes { get; init; }

    /// <summary>Set on the two rows the game allows only one of.</summary>
    public bool UniqueEquipped { get; init; }

    /// <summary>Where the art is, for the icon writer. Never exported: a path reconstructs nothing.</summary>
    public string? IconPath { get; init; }
}

/// <summary>
/// The 480 fixed hero items - starter gear, vendor gear, the default set every hero wears - which
/// are a data table rather than an asset each and so reach the export through nothing else. Every
/// row names itself: Helena's Pauldrons, Aeona's Bindings. None of them exported a display name
/// before, because a gameplay tag with no asset behind it resolves to nothing at all.
///
/// The join to the tag table is the row name, which every one of the 480 rows makes
/// "ItemID.HeroItem.&lt;RowName&gt;" and which matches 480 of 480.
/// </summary>
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

    /// <summary>
    /// Each hero's primary attribute, worked out from the weight templates their own gear names.
    /// A hero's items pull their primary stat from a template named for it - Warmaster's from
    /// AWT_Primary_Stamina_62_Strength_37, Firemage's from the Intellect one - and the template
    /// spells its stats out as ECRItemStat names rather than as ordinals. Stamina is on every
    /// hero's templates and so says nothing; the stat left over once the shared ones are removed
    /// is the hero's own, and every one of the twelve live heroes has exactly one.
    ///
    /// This is the only shipped source that answers for Gunde, whose content lives in a game
    /// feature plugin and who has no row in DT_CharacterInfoTable at all.
    /// </summary>
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

    /// <summary>
    /// The heroes a row is for. Every row names them one of two ways and never both: 414 rows list
    /// the heroes the item is exclusive to and the other 66 name the hero it is the default gear
    /// of, so the two together are the whole attribution and neither alone is.
    /// </summary>
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

    /// <summary>
    /// The stats a weight template holds, each with the share of its pool the template gives it.
    /// A template's weights add up to a hundred: a fixed item has no slots a rarity can add, so
    /// its own stats are the whole of its budget.
    ///
    /// The asset names are stale - AWT_Primary_Stamina_62_Intellect_37 holds 75 and 25 - so the
    /// numbers are read out of the template and never off its name.
    ///
    /// The primary template holds Stamina and the wearer's own attribute and nothing else, so the
    /// entry that is not Stamina is the same base slot a dynamic item spells HeroRelativePrimaryStat
    /// and is scaled the same way.
    /// </summary>
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

    /// <summary>
    /// ECRHeroItemType is not in the usmap, so a row stores its slot as a bare ordinal and
    /// <see cref="ItemSlotNames"/> is what decodes it.
    /// </summary>
    private SlotName? Slot(FStructFallback row) =>
        PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "HeroItemType")?.GenericValue) is { } ordinal
            ? _slots.ByOrdinal(ordinal)
            : null;

    /// <summary>
    /// Where a row's art actually is. ItemIconTexture is a placeholder on 402 of the 480 rows - 312
    /// point at a gem icon and 90 at a rune - and the armour's own icon sits on the visual
    /// collection the row dresses the hero in, under the slot it fills. The two never both hold
    /// something: no row has a visual collection and a real ItemIconTexture, so taking the
    /// collection first and the field second loses nothing and invents nothing. The four slots the
    /// collection has no icon field for - neck, wrists, ring, relic - keep whatever the field says.
    ///
    /// The field is named for the enum rather than for what a player reads, IconWaist and not
    /// IconLegs, which is why the slot arrives here under its ECRHeroItemType name.
    /// </summary>
    private string? Icon(FStructFallback row, string? slot)
    {
        var texture = PropertyWalk.GetString(row, "ItemIconTexture");

        if (slot is null) return texture;
        if (PropertyWalk.GetString(row, "VisualCollection") is not { } collectionPath) return texture;

        var variation = (int)(PropertyWalk.AsNumber(
            PropertyWalk.Get(row, "VisualCollectionSkinVariationIndex")?.GenericValue) ?? 0);

        return _visuals.Icon(collectionPath, variation, slot) ?? texture;
    }

    /// <summary>
    /// Each hero's own stat, taken as the one their gear's primary templates name that not every
    /// hero's do. A hero whose gear leaves that ambiguous is left out and answered for by
    /// <see cref="HeroNames"/>, which fails rather than exporting a hero with no primary stat.
    /// </summary>
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
