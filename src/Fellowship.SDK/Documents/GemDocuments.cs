namespace Fellowship.SDK.Documents;

public sealed class GemTypeDocument : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    public required string Attribute { get; init; }

    public string? Icon { get; init; }

    public string? OverflowDescription { get; init; }

    public IReadOnlyList<Placeholder>? OverflowPlaceholders { get; init; }
}
