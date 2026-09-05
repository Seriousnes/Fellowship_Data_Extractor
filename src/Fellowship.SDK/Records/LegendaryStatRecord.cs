namespace Fellowship.SDK.Records;

public sealed record LegendaryStatRecord : EntityRecord
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
