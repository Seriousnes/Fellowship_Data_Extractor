namespace Fellowship.SDK.Records;

public sealed record TraitPowerRecord : EntityRecord
{
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
