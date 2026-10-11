namespace Fellowship.SDK.Records;

public sealed record RelicPowerRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? CooldownDurationScalesOff { get; init; }
    public IReadOnlyList<string>? CooldownTimeRateScalesOff { get; init; }
    public IReadOnlyList<RecordReference>? CooldownHasteGrantedBy { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public bool? CastTimeScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
