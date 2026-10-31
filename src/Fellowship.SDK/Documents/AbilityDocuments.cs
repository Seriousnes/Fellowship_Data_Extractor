using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class AbilityDocument : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }

    public AbilityCategory? Category { get; init; }


    public EntityReference? NamedAfter { get; init; }

    public EntityReference? PartOf { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? GlobalCooldown { get; init; }
    public bool? GlobalCooldownScalesWithHaste { get; init; }
    public double? Charges { get; init; }
    public IReadOnlyDictionary<string, double>? ChargesByLevelType { get; init; }
    public IReadOnlyList<TargetCountFalloff>? TargetCountFalloff { get; init; }
    public double? MaxTargets { get; init; }
    public IReadOnlyList<string>? CooldownDurationScalesOff { get; init; }
    public IReadOnlyList<string>? CooldownTimeRateScalesOff { get; init; }
    public IReadOnlyList<EntityReference>? CooldownHasteGrantedBy { get; init; }
    public IReadOnlyList<EntityReference>? CooldownAccelerationGrantedBy { get; init; }
    public double? Cost { get; init; }

    public string? Resource { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public double? Range { get; init; }
    public double? MinRange { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    public bool? CastTimeScalesWithHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public IReadOnlyList<EntityReference>? ChangedBy { get; init; }
}
