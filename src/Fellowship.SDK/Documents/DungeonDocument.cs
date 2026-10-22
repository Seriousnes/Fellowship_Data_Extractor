using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class DungeonDocument : EntityDocument
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
    public bool? AvailableForCasual { get; init; }
    public int? MinimumDifficulty { get; init; }
    public IReadOnlyList<int>? Difficulties { get; init; }
    public IReadOnlyList<DungeonFloor>? Floors { get; init; }
    public IReadOnlyList<DungeonPlacement>? Placements { get; init; }
    public IReadOnlyList<IReadOnlyList<WorldPoint>>? Patrols { get; init; }
}
