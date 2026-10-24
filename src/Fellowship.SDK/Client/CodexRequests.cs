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

public sealed record RecordReading
{
    public string? Hero { get; init; }

    public int? ItemLevel { get; init; }

    public int? Upgrades { get; init; }

    public string? Rarity { get; init; }

    public IReadOnlyList<string>? RankSources { get; init; }

    public int? Difficulty { get; init; }

    public int? EquippedTier { get; init; }
}

public sealed record RecordFilter
{
    public string? Q { get; init; }

    public string? Sort { get; init; }

    public string? Dir { get; init; }

    public int? Page { get; init; }

    public int? Size { get; init; }

    public RecordReading? Reading { get; init; }

    public IReadOnlyDictionary<string, string>? Fields { get; init; }
}
