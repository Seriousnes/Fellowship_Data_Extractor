namespace Fellowship.SDK.Documents;

public sealed class RadianceDocument : EntityDocument
{
    public required GemType Type { get; init; }

    public required string Attribute { get; init; }

    public string? Icon { get; init; }

    public string? OverflowDescription { get; init; }

    public IReadOnlyList<Placeholder>? OverflowPlaceholders { get; init; }
}
