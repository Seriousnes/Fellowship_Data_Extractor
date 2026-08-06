namespace Fellowship.SDK.Documents;

public sealed class ItemDocument : EntityDocument
{
    public ArmorType? ArmorType { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }

    public Hero? Hero { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public bool? UniqueEquipped { get; init; }

    public Rarity? Rarity { get; init; }

    public IReadOnlyList<Rarity>? Rarities { get; init; }

    public IReadOnlyList<string>? SetBonus { get; init; }
    public IReadOnlyList<StatSlot>? Stats { get; init; }

    public string? RuleSet { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    public int? GrantsId { get; init; }

    public IReadOnlyList<int>? LegendaryStatIds { get; init; }
}
