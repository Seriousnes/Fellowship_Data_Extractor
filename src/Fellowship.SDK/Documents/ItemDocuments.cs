using Fellowship.SDK;

namespace Fellowship.SDK.Documents;

/// <summary>
/// Something a hero wears or carries. The three discriminators are what make one id several items:
/// id 5146 is a Webbed Robe in cloth, a Tribal Hauberk in leather and a Venom Pitted Chestpiece in
/// plate, and 188 ids read differently per hero this way.
/// </summary>
public sealed class Item : EntityDocument
{
    public ArmorType? ArmorType { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }

    /// <summary>The sole hero this reading is for, and null where it is for several.</summary>
    public Hero? Hero { get; init; }

    /// <summary>Every hero the row is for, which is what a reader is shown.</summary>
    public IReadOnlyList<Hero>? Heroes { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public bool? UniqueEquipped { get; init; }

    /// <summary>The rung every copy of this item is, and none where the drop decides it.</summary>
    public Rarity? Rarity { get; init; }

    public IReadOnlyList<string>? SetBonus { get; init; }
    public IReadOnlyList<StatSlot>? Stats { get; init; }

    /// <summary>
    /// The loot rule set the item is rolled under, without its tag prefix. Which extra slots a
    /// rarity grants the item is stated per rule set, so this is what joins an item to the rarity
    /// table in the vocabulary. The nine spellings this ever states are already the keys of
    /// rarityAdditions in vocabulary.json, so carrying it here publishes nothing new.
    /// </summary>
    public string? RuleSet { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }

    /// <summary>The art at each rarity it differs at, keyed by the rarity's internal name.</summary>
    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    /// <summary>The relic or weapon power this item grants.</summary>
    public int? GrantsId { get; init; }

    /// <summary>The legendary stat lines this item carries. 37 items carry three each.</summary>
    public IReadOnlyList<int>? LegendaryStatIds { get; init; }
}
