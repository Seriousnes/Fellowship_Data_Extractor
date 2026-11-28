using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class CreatureDocument : EntityDocument
{
    public string? Icon { get; init; }
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
    public double? CritChance { get; init; }
    public double? CritMultiplier { get; init; }

    /// <summary>
    /// The creature's base <c>Haste</c>, a multiplier with 1 for no Haste. An ability's time rate is the attribute
    /// itself under <c>ScaleTimeWithHaste</c>.
    /// </summary>
    public double? Haste { get; init; }

    /// <summary>The creature's base <c>AttackSpeed</c>, a multiplier with 1 for no change.</summary>
    public double? AttackSpeed { get; init; }

    public IReadOnlyList<CreatureAbility>? Abilities { get; init; }
    public IReadOnlyList<CreatureAbility>? Passives { get; init; }
    public IReadOnlyList<CreaturePhase>? Phases { get; init; }
    public IReadOnlyList<EntityReference>? FoundIn { get; init; }
}
