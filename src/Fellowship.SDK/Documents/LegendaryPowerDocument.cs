namespace Fellowship.SDK.Documents;

public sealed class LegendaryPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
