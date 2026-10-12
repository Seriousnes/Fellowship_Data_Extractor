using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class RelicPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? CastTime { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? CooldownDurationScalesOff { get; init; }
    public IReadOnlyList<string>? CooldownTimeRateScalesOff { get; init; }
    public IReadOnlyList<EntityReference>? CooldownHasteGrantedBy { get; init; }
    public IReadOnlyList<EntityReference>? CooldownAccelerationGrantedBy { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public bool? CastTimeScalesWithHaste { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}
