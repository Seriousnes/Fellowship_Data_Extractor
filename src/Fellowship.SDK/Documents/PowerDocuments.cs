namespace Fellowship.SDK.Documents;

public sealed class TraitDocument : EntityDocument
{
    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public int? PowerId { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRun>? RankRuns { get; init; }
}
