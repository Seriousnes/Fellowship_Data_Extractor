namespace Fellowship.SDK.Records;

public sealed record EnchantRecord : EntityRecord
{
    public string? PrimaryStat { get; init; }

    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public RecordReference? Power { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<EnchantStatRecord>? Stats { get; init; }
}

public sealed record EnchantStatRecord(string Attribute, double PerRank);

public sealed record TraitRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public RecordReference? Power { get; init; }
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<RankRunRecord>? RankRuns { get; init; }
}
