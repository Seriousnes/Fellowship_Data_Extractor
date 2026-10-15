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

    public IReadOnlyList<RecordReference>? Creatures { get; init; }
}
