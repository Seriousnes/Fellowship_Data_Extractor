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

    /// <summary>The ability whose Blueprint class this ability's class derives from.</summary>
    public EntityReference? ParentClass { get; init; }

    /// <summary>Effects the ability's properties and graph reference.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    /// <summary>Effects and abilities part of the ability or named after it.</summary>
    public IReadOnlyList<EntityReference>? Parts { get; init; }

    /// <summary>Abilities the ability grants.</summary>
    public IReadOnlyList<EntityReference>? Grants { get; init; }

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

    public IReadOnlyList<Threshold>? Thresholds { get; init; }

    public double? Range { get; init; }
    public double? MinRange { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    public bool? CastTimeScalesWithHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }

    /// <summary>Set-by-caller magnitudes the ability computes as a base plus an amount for each unit counted.</summary>
    public IReadOnlyList<SetByCallerScaling>? SetByCallerScaling { get; init; }

    /// <summary>Proc-per-minute rolls the ability makes, each with the rate it is made at.</summary>
    public IReadOnlyList<ProcRoll>? ProcRolls { get; init; }

    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public IReadOnlyList<EntityReference>? Origins { get; init; }

    public IReadOnlyList<EntityReference>? ChangedBy { get; init; }
}
