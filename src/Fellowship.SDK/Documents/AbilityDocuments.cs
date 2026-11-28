using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class AbilityDocument : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }

    public AbilityCategory? Category { get; init; }


    public EntityReference? NameBorrowedFrom { get; init; }

    public EntityReference? NamedAfter { get; init; }

    /// <summary>Abilities the presentation row lists under AdditionalGameplayAbilityAssets.</summary>
    public IReadOnlyList<EntityReference>? AdditionalAbilities { get; init; }

    /// <summary>The listing ability this additional ability replaces through ReplaceAbility.</summary>
    public EntityReference? Replaces { get; init; }

    /// <summary>The listing ability whose SendGameplayEvent triggers this additional ability.</summary>
    public EntityReference? TriggeredBy { get; init; }

    /// <summary>The listing ability whose graph triggers this additional ability through CrTriggerUtilityAbility.</summary>
    public EntityReference? SubcastOf { get; init; }

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

    /// <summary>The flags of the ability's <c>ChannelingData</c>, each from the nearest class of its Blueprint chain with it.</summary>
    public ChannelingTaskData? ChannelingData { get; init; }

    /// <summary>The ability's <c>ActivationBlockedTags</c>. An owner with one of them cannot activate the ability.</summary>
    public IReadOnlyList<string>? ActivationBlockedTags { get; init; }

    /// <summary>A creature ability's <c>TargetAcquisitionPolicy</c> class without the <c>CRAbilityTargetingAcquisitionPolicy_</c> prefix.</summary>
    public string? TargetAcquisition { get; init; }

    /// <summary>A creature ability's <c>TargetSelectionPolicy</c>, the acquisition policy's.</summary>
    public TargetSelection? TargetSelection { get; init; }

    /// <summary>A creature ability's <c>CastingData.Interruptible</c>.</summary>
    public bool? CastInterruptible { get; init; }

    /// <summary>A creature ability's <c>ChannelingData.Interruptible</c>.</summary>
    public bool? ChannelInterruptible { get; init; }

    /// <summary>A creature ability's <c>TargetedCastingData.ChangeTargetOnTaunt</c>.</summary>
    public bool? ChangeTargetOnTaunt { get; init; }

    /// <summary>A creature ability with the tag <c>AbilityType.AutoAttack</c>.</summary>
    public bool? AutoAttack { get; init; }

    /// <summary>A creature ability with the tag <c>AbilityType.Aoe</c>.</summary>
    public bool? Area { get; init; }

    /// <summary>A creature ability that spawns an actor with a projectile movement component.</summary>
    public bool? Projectile { get; init; }

    /// <summary>The ability's <c>AbilityTags</c>.</summary>
    public IReadOnlyList<string>? AbilityTags { get; init; }

    /// <summary>
    /// A creature ability's <c>BlockAbilitiesWithTag</c>. From its activation to its <c>K2_EndAbility</c> the ability
    /// blocks the activation of each ability of the same creature whose <see cref="AbilityTags"/> has one of these tags
    /// or a tag under one: <c>Ability</c> blocks <c>Ability.AutoAttack</c>.
    /// </summary>
    public IReadOnlyList<string>? BlockAbilitiesWithTag { get; init; }

    /// <summary>
    /// The ability's <c>ScaleTimeWithAttackSpeed</c>. <see cref="CastTimeScalesWithHaste"/> is its
    /// <c>ScaleTimeWithHaste</c>. The two set the ability's time factor: the owner's <c>Haste</c> for the first, plus
    /// <c>AttackSpeed</c> minus 1 for the second, and 1 for an ability with neither.
    /// </summary>
    public bool? ScaleTimeWithAttackSpeed { get; init; }

    /// <summary>A creature ability's <c>InterruptCooldownDuration</c> in seconds.</summary>
    public double? InterruptCooldownDuration { get; init; }

    /// <summary>
    /// The routes of a creature ability's graph from its activation to <c>K2_EndAbility</c>, each the tasks it waits on
    /// in order. A <c>WaitDelayCR</c> or <c>Delay</c> directly before the end is the time the ability stays active after
    /// its hits.
    /// </summary>
    public IReadOnlyList<EndAbilityPath>? EndAbilityPaths { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }

    /// <summary>Set-by-caller magnitudes the ability computes as a base plus an amount for each unit counted.</summary>
    public IReadOnlyList<SetByCallerScaling>? SetByCallerScaling { get; init; }

    /// <summary>
    /// Each delta the ability assigns to a target's Stagger through an effect running
    /// <c>CRModifyStaggerExecution</c>.
    /// </summary>
    public IReadOnlyList<StaggerDelta>? StaggerDeltas { get; init; }

    /// <summary>Proc-per-minute rolls the ability makes, each with the rate it is made at.</summary>
    public IReadOnlyList<ProcRoll>? ProcRolls { get; init; }

    /// <summary>The waits the Blueprint holds on abilities being committed.</summary>
    public IReadOnlyList<CommitTrigger>? CommitTriggers { get; init; }

    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public IReadOnlyList<EntityReference>? Origins { get; init; }

    public IReadOnlyList<EntityReference>? ChangedBy { get; init; }
}
