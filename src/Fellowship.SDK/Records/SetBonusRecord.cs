namespace Fellowship.SDK.Records;

public sealed record SetBonusRecord : EntityRecord
{
    public int? Pieces { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public RecordReference? Power { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<PrimaryStat>? AllowedAttributes { get; init; }
}
