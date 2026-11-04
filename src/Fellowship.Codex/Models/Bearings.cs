using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record Bearings
{
    public EntityDocument? Entity { get; init; }

    public IReadOnlyList<EntityDocument> Readings { get; init; } = [];

    public string? Reading { get; init; }

    public TooltipLink? Tip { get; init; }

    public string Page { get; init; } = string.Empty;

    public string Under { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Noun { get; init; } = string.Empty;

    public int Id { get; init; }

    public IReadOnlyList<(string Label, string Href)> Related { get; init; } = [];

    /// <summary>Query keys present with an empty value, such as an emptied number box, so the box renders empty on every render.</summary>
    public IReadOnlySet<string> Emptied { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
