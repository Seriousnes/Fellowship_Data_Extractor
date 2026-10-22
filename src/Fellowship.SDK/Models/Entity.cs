using System.Text.Json.Serialization;

using Fellowship.SDK.Models;

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

    public IReadOnlyList<IconSource>? SetBonus { get; init; }

    public int? SetCount { get; init; }

    public bool? UniqueEquipped { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }

    public IReadOnlyList<string>? EffectTags { get; init; }

    public IReadOnlyList<StatSlot>? Stats { get; init; }

    public string? RuleSet { get; init; }

    public string? TraitTree { get; init; }

    public IReadOnlyList<string>? Rarities { get; init; }

    public string? Description { get; init; }

    public string? Acquire { get; init; }

    public string? TitleText { get; init; }

    public string? Lore { get; init; }

    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }

    public IReadOnlyList<string>? Detail { get; init; }

    public string? Icon { get; init; }

    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    public IconSource? IconInheritedFrom { get; init; }

    public IconSource? NameInheritedFrom { get; init; }

    public IconSource? NamedAfter { get; init; }

    public string? Category { get; init; }

    public bool? EnabledInGame { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public IReadOnlyDictionary<string, double>? ChargesByLevelType { get; init; }
    public IReadOnlyList<TargetCountFalloffSource>? TargetCountFalloff { get; init; }
    public double? MaxTargets { get; init; }
    public double? Cost { get; init; }

    public string? Resource { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    [JsonIgnore]
    public string? Tuning { get; init; }

    [JsonIgnore]
    public IReadOnlyList<string>? Declares { get; init; }

    public double? Range { get; init; }
    public double? MinRange { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    public bool? CastTimeScalesWithHaste { get; init; }

    public IReadOnlyList<string>? CooldownDurationScalesOff { get; init; }

    public IReadOnlyList<string>? CooldownTimeRateScalesOff { get; init; }

    public IReadOnlyList<int>? CooldownHasteGrantedBy { get; init; }

    public IReadOnlyList<int>? CooldownAccelerationGrantedBy { get; init; }

    public bool? TickrateIgnoresHaste { get; init; }

    public int? MaxStacks { get; init; }

    public bool? Infinite { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyDictionary<string, Placeholder>? Placeholders { get; init; }

    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<double?>>? ConstantsByRank { get; init; }

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

    public int? AbilityMaxRank { get; init; }

    public IReadOnlyList<string>? PoolSlots { get; init; }

    public int? AbilityRank { get; init; }

    public string? IconHovered { get; init; }

    public IReadOnlyDictionary<string, string>? NameByHero { get; init; }

    public IReadOnlyList<TraitStat>? TraitStats { get; init; }

    public int? RandomChance { get; init; }

    public IReadOnlyList<RankRun>? RankRuns { get; init; }

    public IReadOnlyList<string>? AllowedRoles { get; init; }

    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    public Entity? Grants { get; init; }

    public IReadOnlyList<Entity>? LegendaryStats { get; init; }

    public int? GrantsId { get; init; }

    public IReadOnlyList<int>? LegendaryStatIds { get; init; }

    public bool? SlotBorneTalent { get; init; }

    public IReadOnlyList<ItemSource>? Sources { get; init; }

    public int? ItemLevel { get; init; }

    public IReadOnlyList<string>? OwningHeroes { get; init; }

    public IconSource? Owner { get; init; }

    public IconSource? AppliedBy { get; init; }

    public IReadOnlyList<IconSource>? Granted { get; init; }

    public IReadOnlyList<string>? ApplierTags { get; init; }

    public CreatureRank? Rank { get; init; }

    public double? Health { get; init; }

    public double? Armor { get; init; }

    public double? Resist { get; init; }

    public int? KillScore { get; init; }

    public int? SpiritPoints { get; init; }

    public bool? CountsTowardScore { get; init; }

    public double? Intellect { get; init; }

    public double? Strength { get; init; }

    public double? Agility { get; init; }

    public string? Scaling { get; init; }

    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    public IReadOnlyList<PhaseAbility>? Abilities { get; init; }

    public IReadOnlyList<PhaseAbility>? Passives { get; init; }

    public IReadOnlyList<IconSource>? FoundIn { get; init; }

    public string? Background { get; init; }

    public int? MaxPlayers { get; init; }

    public double? QualifierTime { get; init; }

    public int? RequiredScore { get; init; }

    public int? MinimumHeroRank { get; init; }

    public string? LevelType { get; init; }

    public bool? SeasonalPinnacle { get; init; }

    public int? MinimumDifficulty { get; init; }

    public IReadOnlyList<int>? Difficulties { get; init; }

    public IReadOnlyList<DungeonFloor>? Floors { get; init; }

    public IReadOnlyList<PlacedCreature>? Placements { get; init; }

    public IReadOnlyList<IReadOnlyList<WorldPoint>>? Patrols { get; init; }

    public string? ModifierKind { get; init; }

    public string? HeroTitle { get; init; }

    public string? HeroColor { get; init; }

    public string? Biography { get; init; }

    public string? Playstyle { get; init; }

    public string? SpiritAbility { get; init; }

    public string? Role { get; init; }

    public string? RoleIcon { get; init; }

    public int? OverallDifficulty { get; init; }

    public double? Offense { get; init; }

    public double? Defense { get; init; }

    public double? Control { get; init; }

    public IReadOnlyDictionary<string, double>? BaseAttributes { get; init; }

    [JsonIgnore]
    public IReadOnlyList<Variant>? Variants { get; init; }
}

public sealed record IconSource(string Table, int NetId, string Name);

public sealed record PhaseAbility(IconSource Ability, string Phase, bool Shown)
{
    public bool? EnabledByDefault { get; init; }

    public PhaseCooldown? InitialCooldown { get; init; }

    public PhaseCooldown? Cooldown { get; init; }

    public PhaseCooldown? InterruptCooldown { get; init; }

    public PhaseCooldown? CCCooldown { get; init; }

    public PhaseCooldown? PostAbilityPadding { get; init; }
}

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
    public int? GrantsId { get; set; }
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

    public bool? Multiplies { get; init; }

    public string? Format { get; init; }

    public string? Unit { get; init; }

    public bool? ShowDamageType { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyDictionary<string, object?>? Constants { get; init; }

    public bool? FromOwnConstants { get; init; }

    public bool? Tickrate { get; init; }

    public bool? IgnoresHaste { get; init; }

    public bool? ScalesWithExpertise { get; init; }

    public PlaceholderBracket? Bracket { get; init; }

    [JsonIgnore]
    public TokenQuantity Quantity { get; init; }

    [JsonIgnore]
    public double? PresetSpread { get; init; }

    [JsonIgnore]
    public bool PresetSpreadOverridden { get; init; }

    [JsonIgnore]
    public int? DynamicTextFloatIdentifier { get; init; }
}

public sealed record PlaceholderTerm(string Attribute, double Coefficient);

public sealed record PlaceholderBracket(IReadOnlyList<PlaceholderTerm> Terms, double Addend);

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
    Unknown,

    Period,

    Duration,

    ExecutionMagnitude,
}

public sealed record TargetCountFalloffSource(IconSource? Effect, double Threshold, string? Row);
