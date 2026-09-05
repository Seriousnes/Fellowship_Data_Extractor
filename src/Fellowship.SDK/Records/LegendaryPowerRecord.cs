namespace Fellowship.SDK.Records;

public sealed record LegendaryPowerRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
