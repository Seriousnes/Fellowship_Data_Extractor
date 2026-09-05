namespace Fellowship.SDK.Records;

public sealed record RelicPowerRecord : EntityRecord
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
