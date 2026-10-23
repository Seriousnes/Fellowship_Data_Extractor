namespace Fellowship.SDK.Records;

public abstract record AugmentationRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<RankRunRecord>? RankRuns { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<RecordReference>? Grants { get; init; }
}

public sealed record SigilRecord : AugmentationRecord;

public sealed record AspectRecord : AugmentationRecord;

public sealed record ProtectiveEnchantRecord : AugmentationRecord;

public sealed record EnchantRecord : AugmentationRecord
{
    public string? PrimaryStat { get; init; }

    public IReadOnlyList<EnchantStatRecord>? Stats { get; init; }
}

public sealed record EnchantStatRecord(string Attribute, double PerRank);
