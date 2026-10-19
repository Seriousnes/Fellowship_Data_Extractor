namespace Fellowship.SDK.Documents;

public sealed class MountDocument : EntityDocument
{
    public string? Description { get; init; }

    public string? Lore { get; init; }

    public string? Icon { get; init; }

    public Rarity? Rarity { get; init; }

    public string? Acquire { get; init; }
}
