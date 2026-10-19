namespace Fellowship.SDK.Records;

public sealed record MountRecord : EntityRecord
{
    public string? Description { get; init; }

    public string? Lore { get; init; }

    public string? Icon { get; init; }

    public string? Rarity { get; init; }

    public string? Acquire { get; init; }
}
