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
}
