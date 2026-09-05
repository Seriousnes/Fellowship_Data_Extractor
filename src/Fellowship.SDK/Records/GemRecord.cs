namespace Fellowship.SDK.Records;

public sealed record GemRecord : EntityRecord
{
    public required GemType GemType { get; init; }

    public required int Power { get; init; }

    public string? Rarity { get; init; }

    public string? Icon { get; init; }

    public GemOverflowRecord? GemOverflow { get; init; }
}
