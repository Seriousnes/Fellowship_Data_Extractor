namespace Fellowship.SDK.Records;

public sealed record ModifierRecord : EntityRecord
{
    public string? Description { get; init; }

    public string? Icon { get; init; }

    public string? Kind { get; init; }

    public int? MinimumHeroRank { get; init; }
}
