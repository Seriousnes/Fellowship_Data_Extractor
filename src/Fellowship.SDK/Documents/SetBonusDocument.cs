namespace Fellowship.SDK.Documents;

public sealed class SetBonusDocument : EntityDocument
{
    public int? Pieces { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int? PowerId { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<PrimaryStat>? AllowedAttributes { get; init; }
}
