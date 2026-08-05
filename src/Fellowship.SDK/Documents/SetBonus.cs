namespace Fellowship.SDK.Documents;

/// <summary>
/// A bonus for wearing several pieces of one set. Its mechanics live in the set bonus power it
/// points at, and 3 of the rows are gated to a role rather than offered to everyone. 21 of the 23
/// rows state how many pieces they need and 2 state nothing at all.
/// </summary>
public sealed class SetBonus : EntityDocument
{
    public int? Pieces { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public int? PowerId { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<PrimaryStat>? AllowedAttributes { get; init; }
}
