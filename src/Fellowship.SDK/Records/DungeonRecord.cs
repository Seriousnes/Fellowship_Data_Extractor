using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public sealed record DungeonRecord : EntityRecord
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public string? Background { get; init; }
    public int? MaxPlayers { get; init; }
    public double? QualifierTime { get; init; }
    public int? RequiredScore { get; init; }
    public int? MinimumHeroRank { get; init; }
    public string? LevelType { get; init; }
    public bool? SeasonalPinnacle { get; init; }
    public int? MinimumDifficulty { get; init; }
    public IReadOnlyList<int>? Difficulties { get; init; }

    public IReadOnlyList<RecordReference>? Creatures { get; init; }

    public IReadOnlyList<DungeonFloor>? Floors { get; init; }
    public IReadOnlyList<DungeonPlacementRecord>? Placements { get; init; }
    public IReadOnlyList<IReadOnlyList<WorldPoint>>? Patrols { get; init; }
}

public sealed record DungeonPlacementRecord(RecordReference Creature, WorldPoint Position)
{
    public int? Floor { get; init; }

    public double? MapX { get; init; }

    public double? MapY { get; init; }

    public int? Patrol { get; init; }
}
