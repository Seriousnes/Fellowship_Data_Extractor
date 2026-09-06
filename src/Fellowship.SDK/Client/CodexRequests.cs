namespace Fellowship.SDK.Client;

public sealed record EntityFilter
{
    public string? Q { get; init; }

    public IReadOnlyList<EntityType>? Type { get; init; }

    public string? Sort { get; init; }

    public string? Dir { get; init; }

    public string? Group { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public IReadOnlyDictionary<string, string>? Facets { get; init; }
}
