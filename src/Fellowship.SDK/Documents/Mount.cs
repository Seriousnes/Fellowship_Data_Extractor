namespace Fellowship.SDK.Documents;

/// <summary>A mount a player rides. Its mechanics live in the mount power it pairs with.</summary>
public sealed class Mount : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
}
