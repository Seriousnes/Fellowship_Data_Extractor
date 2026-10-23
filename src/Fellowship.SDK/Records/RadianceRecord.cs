namespace Fellowship.SDK.Records;

public sealed record RadianceRecord : EntityRecord
{
    public required GemType GemType { get; init; }

    public required string Attribute { get; init; }

    public string? Icon { get; init; }

    public string? OverflowDescription { get; init; }

    public IReadOnlyList<PlaceholderRecord>? OverflowPlaceholders { get; init; }
}
