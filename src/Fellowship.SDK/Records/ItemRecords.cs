namespace Fellowship.SDK.Records;

public sealed record ItemRecord : EntityRecord
{
    public ArmorType? ArmorType { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }

    public Hero? Hero { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public bool? UniqueEquipped { get; init; }
    public bool? EnabledInGame { get; init; }

    public string? Rarity { get; init; }

    public IReadOnlyList<string>? Rarities { get; init; }

    public IReadOnlyList<StatSlotRecord>? Stats { get; init; }

    public string? RuleSet { get; init; }

    public AttunementTreeRecord? AttunementTree { get; init; }

    public string? Description { get; init; }

    public string? Acquire { get; init; }

    public string? TitleText { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    public RecordReference? Grants { get; init; }

    public IReadOnlyList<LegendaryStatRecord>? LegendaryStats { get; init; }

    public IReadOnlyList<Models.LegendaryStatText>? UntaggedLegendaryStats { get; init; }

    public IReadOnlyList<ItemSourceRecord>? Sources { get; init; }

    public int? ItemLevel { get; init; }

    public ItemMagnitudesRecord? Magnitudes { get; init; }
}

public sealed record ItemMagnitudesRecord
{
    public int ItemLevel { get; init; }

    public int Upgrades { get; init; }

    public int? MaxUpgrades { get; init; }

    public string? Rarity { get; init; }

    public decimal? PotentialPower { get; init; }

    public IReadOnlyList<StatMagnitudeRecord> Stats { get; init; } = [];
}
