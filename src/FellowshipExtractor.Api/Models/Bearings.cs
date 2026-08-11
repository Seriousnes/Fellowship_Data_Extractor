using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record Bearings
{
    public EntityDocument? Shown { get; init; }

    public IReadOnlyList<EntityDocument> Readings { get; init; } = [];

    public string? Reading { get; init; }

    public string Address { get; init; } = string.Empty;

    public string Page { get; init; } = string.Empty;

    public string Under { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Noun { get; init; } = string.Empty;

    public int Id { get; init; }

    public IReadOnlyList<(string Label, string Href, int Count)> Related { get; init; } = [];

    public IReadOnlyDictionary<string, string> Asked { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
