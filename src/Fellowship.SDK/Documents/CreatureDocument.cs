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
    public IReadOnlyList<CreatureAbility>? Abilities { get; init; }
    public IReadOnlyList<CreatureAbility>? Passives { get; init; }
    public IReadOnlyList<EntityReference>? FoundIn { get; init; }
}
