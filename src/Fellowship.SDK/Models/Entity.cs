using System.Text.Json.Serialization;

namespace Fellowship.SDK;

public sealed record Entity
{
    public string? Table { get; init; }

    public EntityType? Type { get; init; }

    public int? NetId { get; init; }
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? Slot { get; init; }

    public bool? GemSlot { get; init; }

    public string? ArmorType { get; init; }

    public string? PrimaryStat { get; init; }

    public IReadOnlyList<string>? Heroes { get; init; }

    public IReadOnlyList<string>? SetBonus { get; init; }

    public int? SetCount { get; init; }

    public bool? UniqueEquipped { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyList<StatSlot>? Stats { get; init; }

    public string? RuleSet { get; init; }

    public IReadOnlyList<string>? Rarities { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string>? Detail { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    public IconSource? IconInheritedFrom { get; init; }

    public IconSource? NameInheritedFrom { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public double? Cost { get; init; }

    public string? Resource { get; init; }

    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    public bool? ScalesWithHaste { get; init; }

    public bool? TickrateIgnoresHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public IReadOnlyDictionary<string, string>? TalentGates { get; init; }

    public IReadOnlyList<string>? GrantedTags { get; init; }

    public string? TalentSlot { get; init; }

    public int? TalentRow { get; init; }

    public int? TalentColumn { get; init; }

    public int? TalentCost { get; init; }

    public int? Augmentation { get; init; }

    public string? GemAttribute { get; init; }

    public GemOverflow? GemOverflow { get; init; }

    public string? GemType { get; init; }

    public int? GemTier { get; init; }

    public int? GemPower { get; init; }

    public string? Rarity { get; init; }

    public int? RarityTier { get; init; }

    public int? GemNode { get; init; }

    public int? RequiredGemPower { get; init; }

    public int? Replaces { get; init; }

    public string? TraitCategoryTag { get; init; }

    public string? TraitCategory { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public IReadOnlyList<RankRun>? RankRuns { get; init; }

    public IReadOnlyList<string>? AllowedRoles { get; init; }

    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    public Entity? Grants { get; init; }

    public IReadOnlyList<Entity>? LegendaryStats { get; init; }

    public int? GrantsId { get; init; }

    public IReadOnlyList<int>? LegendaryStatIds { get; init; }

    public bool? SlotBorneTalent { get; init; }

    [JsonIgnore]
    public IReadOnlyList<Variant>? Variants { get; init; }
}

public sealed record IconSource(string Table, int NetId, string Name);

public sealed record GemOverflow(string Description)
{
    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public string? Icon { get; init; }
}

public sealed record RankRun(string Literal, IReadOnlyList<string> Printed)
{
    public IReadOnlyList<double?>? Values { get; init; }

    public string? Key { get; init; }

    public string? Transform { get; init; }

    public int? Ticks { get; init; }

    public bool? Disagrees { get; init; }
}

public sealed record Variant(string Hero)
{
    public string? HeroName { get; init; }
    public string? DisplayName { get; set; }
    public Entity? Grants { get; set; }
}

public sealed record Placeholder
{
    public string? Key { get; init; }

    public string? Attribute { get; init; }

    public double? Coefficient { get; init; }

    public double? Spread { get; init; }

    public double? Value { get; init; }

    public string? Text { get; init; }

    public bool? Percent { get; init; }

    public string? Format { get; init; }

    public string? Unit { get; init; }

    public bool? ShowDamageType { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    public bool? FromOwnConstants { get; init; }

    public bool? Tickrate { get; init; }

    public bool? IgnoresHaste { get; init; }

    [JsonIgnore]
    public TokenQuantity Quantity { get; init; }

    [JsonIgnore]
    public double? PresetSpread { get; init; }

    [JsonIgnore]
    public int? DynamicTextFloatIdentifier { get; init; }
}

public static class SlotKinds
{
    public const string Primary = "primary";

    public const string Fixed = "fixed";

    public const string Random = "random";
}

public sealed record StatSlot(string Stat, int Weight)
{
    public required string Kind { get; init; }
}

public enum TokenQuantity
{
    Unsettled,

    Period,

    Duration,

    ExecutionMagnitude,
}
