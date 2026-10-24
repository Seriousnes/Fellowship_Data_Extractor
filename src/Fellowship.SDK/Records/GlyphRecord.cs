namespace Fellowship.SDK.Records;

public sealed record GlyphRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public int? MaxRank { get; init; }

    public IReadOnlyList<string>? Slots { get; init; }

    public int? AbilityRank { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<double?>>? Constants { get; init; }

    public EquippedGlyphRecord? Equipped { get; init; }
}

public sealed record EquippedGlyphRecord(int Tier, int Tiers)
{
    public string? Description { get; init; }
}
