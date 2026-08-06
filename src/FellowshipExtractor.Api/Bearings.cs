using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed record Bearings
{
    public Entity Shown { get; init; } = new();

    public IReadOnlyList<Entity> Readings { get; init; } = [];

    public string? Reading { get; init; }

    public string Address { get; init; } = string.Empty;

    public string Page { get; init; } = string.Empty;

    public string Under { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Noun { get; init; } = string.Empty;

    public int NetId { get; init; }

    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; init; } = [];
}
