namespace Fellowship.SDK.Documents;

/// <summary>An ability a relic grants. 11 of them, all named.</summary>
public sealed class RelicPower : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public bool? ScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
