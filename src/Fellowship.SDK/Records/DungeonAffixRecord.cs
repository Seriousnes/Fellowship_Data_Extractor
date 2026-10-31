namespace Fellowship.SDK.Records;

public sealed record DungeonAffixRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int? MinimumHeroRank { get; init; }

    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
}
