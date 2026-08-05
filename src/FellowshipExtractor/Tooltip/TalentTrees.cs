using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One live talent: where it sits in its hero's grid, what the tree says it is, and the
/// augmentation that carries its mechanics.
/// </summary>
public sealed record Talent(string HeroName, string Slot, int Row, int Column, int Cost)
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? IconPath { get; init; }

    /// <summary>The augmentation's netId, or null for the one talent that names no augmentation.</summary>
    public int? Augmentation { get; init; }
}

/// <summary>
/// Every hero's talent grid, read from the twelve CRTalentData assets that define them.
///
/// Membership in one of those assets' Talents arrays is the only definition of a talent. Asset
/// naming is not: 373 augmentations carry "Talent" in the name against 179 that are live, and 36
/// live talents are named CAA_&lt;Hero&gt;_TraitN instead.
///
/// A row is live when two things hold and neither alone is enough. DisabledInGame alone leaves five
/// retired stragglers behind; the slot filter alone keeps Warmaster's "Commander's Advance
/// (disabled)". The slot filter is derived rather than listed: a Talent.ID tag is a live slot when
/// its own numeric suffix equals its netId, which the shipped table satisfies for TalentN 1 to 21
/// and for none of the retired Talent50, Talent84 and Talent90-99.
/// </summary>
public sealed class TalentTrees
{
    public const string SlotTable = "Talent.ID";
    public const string AugmentationTable = "CRCharacterAugmentationAsset";

    private const string TalentDataClass = "CRTalentData";
    private const string AssetPrefix = "DA_Talents_";
    private const string TalentsField = "Talents";
    private const string SlotPrefix = "Talent.ID.Talent";

    private readonly Dictionary<string, Talent> _byAugmentation = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, List<Talent>> _bySlot = [];

    /// <summary>How many live talents the twelve trees hold between them.</summary>
    public int Count { get; }

    /// <summary>How many heroes ship a tree.</summary>
    public int HeroCount { get; }

    public TalentTrees(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyList<TagRecord> tags,
        HeroNames heroes)
    {
        var slots = LiveSlots(tags);
        var augmentations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (tag.Table.Equals(AugmentationTable, StringComparison.OrdinalIgnoreCase))
            {
                augmentations.TryAdd(tag.Name, tag.NetId);
            }
        }

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(TalentDataClass, StringComparison.Ordinal)) continue;

            var codename = asset.AssetName.Text;
            if (!codename.StartsWith(AssetPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{codename} is a {TalentDataClass} whose name does not begin {AssetPrefix}, so the hero it " +
                    "belongs to cannot be read from it. Every hero's tree is named after them.");
            }

            codename = codename[AssetPrefix.Length..];

            if (heroes.Profile(codename) is not { } hero)
            {
                throw new InvalidOperationException(
                    $"The talent tree names hero {codename}, which no metadata asset ships. Their whole grid " +
                    "would export without a hero on it.");
            }

            HeroCount++;
            Count += Read(provider, asset.ObjectPath, hero, slots, augmentations);
        }

        if (Count == 0)
        {
            throw new InvalidOperationException(
                $"No {TalentDataClass} asset yielded a live talent. Every talent would export as whatever the " +
                "ability it modifies happens to say.");
        }
    }

    /// <summary>The talent an augmentation asset carries, or null when it carries none.</summary>
    public Talent? ForAugmentation(string assetName) => _byAugmentation.GetValueOrDefault(assetName);

    /// <summary>Every hero's talent in one slot. A slot is a coordinate and holds a different talent per hero.</summary>
    public IReadOnlyList<Talent> InSlot(int netId) =>
        _bySlot.TryGetValue(netId, out var talents) ? talents : [];

    /// <summary>
    /// One Talent.ID slot spread into the talents that occupy it, one line per hero. The slot tag
    /// says nothing on its own: Talent8 is Ardeos's "Rolling Flames" at row 5 column 1 and Mara's
    /// "Macabre Stratagem" at row 4 column 1, and all 19 live slots differ this way.
    ///
    /// A line points at the augmentation holding its name, text and art rather than repeating them.
    /// The one talent that names no augmentation has nowhere to point and carries its own record.
    /// </summary>
    public static List<Entity> Expand(Entity slot, IReadOnlyList<Talent> talents, IconWriter? icons) =>
    [
        .. talents
            .OrderBy(talent => talent.HeroName, StringComparer.Ordinal)
            .Select(talent => slot with
            {
                DisplayName = talent.Augmentation is null ? talent.DisplayName : slot.DisplayName,
                Description = talent.Augmentation is null ? talent.Description : slot.Description,
                Icon = talent.Augmentation is null && talent.IconPath is { } path
                    ? icons?.Invoke(path)
                    : slot.Icon,
                SlotBorneTalent = talent.Augmentation is null ? true : null,
                Heroes = [talent.HeroName],
                TalentRow = talent.Row,
                TalentColumn = talent.Column,
                TalentCost = talent.Cost,
                Augmentation = talent.Augmentation,
            }),
    ];

    /// <summary>
    /// The slots a tree can still place a talent in, by netId. The shipped table numbers its live
    /// tags in sequence and renumbers a retired one away from its netId, so the two agreeing is the
    /// table's own record of which slots are still in use.
    /// </summary>
    private static Dictionary<string, int> LiveSlots(IReadOnlyList<TagRecord> tags)
    {
        var live = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (!tag.Table.Equals(SlotTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (!tag.Name.StartsWith(SlotPrefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(tag.Name[SlotPrefix.Length..], out var suffix)) continue;
            if (suffix != tag.NetId) continue;

            live[tag.Name] = tag.NetId;
        }

        if (live.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {SlotTable} tag numbers itself after its own netId, so nothing says which slots are live " +
                "and every retired talent would export beside the one that replaced it.");
        }

        return live;
    }

    private int Read(
        AbstractFileProvider provider,
        string objectPath,
        HeroProfile hero,
        IReadOnlyDictionary<string, int> slots,
        IReadOnlyDictionary<string, int> augmentations)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            throw new InvalidOperationException(
                $"{objectPath} would not load. {hero.ShippedName}'s eighteen talents would export as whatever " +
                "the abilities they modify happen to say.");
        }

        var data = package.GetExports()
            .FirstOrDefault(e => e.Name.Equals(AbilityPresentationIndex.AssetName(objectPath), StringComparison.OrdinalIgnoreCase))
            ?? package.GetExports().FirstOrDefault();

        if (data is null || PropertyWalk.Get(data, TalentsField)?.GenericValue is not UScriptArray rows)
        {
            throw new InvalidOperationException(
                $"{objectPath} holds no {TalentsField} array, so {hero.ShippedName} has no grid to read.");
        }

        // Column is the ordinal within TalentRow taken from array order. There is no TalentColumn
        // field and TalentID is a slot rather than a position, so array order is the only record of
        // where in its row a talent sits. What licenses reading it that way is that the live rows
        // of every hero are the front of the array in row-major order, and Placement stops the
        // export rather than guessing a grid if a patch breaks that.
        var live = new List<(int Index, IPropertyHolder Fields, string Slot, int GridRow)>();

        for (var index = 0; index < rows.Properties.Count; index++)
        {
            if (rows.Properties[index]?.GenericValue is not FScriptStruct { StructType: IPropertyHolder row }) continue;
            if (PropertyWalk.Get(row, "DisabledInGame")?.GenericValue is true) continue;
            if (PropertyWalk.GetString(row, "TalentID") is not { } slot) continue;
            if (!slots.ContainsKey(slot)) continue;
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "TalentRow")?.GenericValue) is not { } gridRow) continue;

            live.Add((index, row, slot, gridRow));
        }

        Placement(hero, live);

        var column = 0;
        var previous = 0;

        foreach (var (_, row, slot, gridRow) in live)
        {
            column = gridRow == previous ? column + 1 : 1;
            previous = gridRow;

            var augmentation = PropertyWalk.GetString(row, "CharacterAugmentationAsset") is { } path
                ? AbilityPresentationIndex.AssetName(path)
                : null;

            int? netId = augmentation is not null && augmentations.TryGetValue(augmentation, out var tagged)
                ? tagged
                : null;

            var talent = new Talent(hero.ShippedName, slot, gridRow, column, Cost(row, hero, slot))
            {
                DisplayName = PropertyWalk.GetText(row, "Name"),
                Description = PropertyWalk.GetText(row, "DetailedDescription"),
                IconPath = PropertyWalk.GetStruct(row, "UIIconTexture") is { } brush
                    ? PropertyWalk.GetString(brush, "ResourceObject")
                    : null,
                Augmentation = netId,
            };

            // Five augmentations are named by both a live row and a retired one. Only live rows
            // reach here, so Gunde's real "Murder of Crows" cannot lose to the retired draft of it.
            if (augmentation is not null && netId is not null) _byAugmentation[augmentation] = talent;

            if (!_bySlot.TryGetValue(slots[slot], out var sharing)) _bySlot[slots[slot]] = sharing = [];
            sharing.Add(talent);
        }

        return live.Count;
    }

    /// <summary>
    /// The point cost the row declares. Read rather than computed from the row number: the live
    /// talents happen to cost 2/1/2/1/3/1 by row in all twelve trees, and the retired rows already
    /// break that pattern, so a rule inferred from the live half would be wrong the moment one of
    /// them came back.
    /// </summary>
    private static int Cost(IPropertyHolder row, HeroProfile hero, string slot) =>
        PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "TalentPointCost")?.GenericValue)
        ?? throw new InvalidOperationException(
            $"{hero.ShippedName}'s talent in {slot} declares no TalentPointCost, and nothing else in the build " +
            "says what a talent costs.");

    /// <summary>
    /// That a hero's live talents really do form a grid. They must be the front of the array with
    /// nothing retired interleaved, and their rows must not go backwards, because otherwise the
    /// ordinal within a row is not a column and the whole placement is a guess.
    /// </summary>
    private static void Placement(
        HeroProfile hero, IReadOnlyList<(int Index, IPropertyHolder Fields, string Slot, int GridRow)> live)
    {
        for (var i = 0; i < live.Count; i++)
        {
            if (live[i].Index == i && (i == 0 || live[i].GridRow >= live[i - 1].GridRow)) continue;

            throw new InvalidOperationException(
                $"{hero.ShippedName}'s live talents are not the front of their tree in row-major order: entry {i} " +
                $"sits at array index {live[i].Index} in row {live[i].GridRow}. A talent's column is its ordinal " +
                "within its row in array order and that reading no longer holds.");
        }
    }
}
