using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public sealed record CreatureRecord : EntityRecord
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
    public IReadOnlyList<CreatureAbilityRecord>? Abilities { get; init; }
    public IReadOnlyList<CreatureAbilityRecord>? Passives { get; init; }
    public IReadOnlyList<RecordReference>? FoundIn { get; init; }

    public CreatureDifficultyRecord? Difficulty { get; init; }
}

public sealed record CreatureDifficultyRecord(int Difficulty, string Label)
{
    public double? Health { get; init; }
}
