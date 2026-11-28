using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class EffectDocument : EntityDocument
{
    public string? Description { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyList<string>? EffectTags { get; init; }

    /// <summary>The tags the effect's target has while the effect is active: its <c>TargetTagsGameplayEffectComponent</c>'s granted tags.</summary>
    public IReadOnlyList<string>? GrantedTags { get; init; }

    /// <summary>The calculation class of each of the effect's <c>Executions</c>, without its package.</summary>
    public IReadOnlyList<string>? Executions { get; init; }

    public double? Duration { get; init; }

    /// <summary>The attribute the duration is a share of, where no one number states it.</summary>
    public DurationScaling? DurationScaling { get; init; }

    public double? Period { get; init; }
    public double? Cooldown { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    public bool? TickrateIgnoresHaste { get; init; }

    public int? MaxStacks { get; init; }

    /// <summary>
    /// Periodic executions over the duration: one per whole period, and one more on application unless the effect
    /// declares it does not execute then. An effect of the <c>HasDuration</c> policy alone has them.
    /// </summary>
    public int? Ticks { get; init; }

    public bool? Infinite { get; init; }

    /// <summary>
    /// The effect's <c>DurationPolicy</c>, a member of <c>EGameplayEffectDurationType</c>: <c>Instant</c>,
    /// <c>Infinite</c> or <c>HasDuration</c>. An <c>Instant</c> effect executes once for each application, so a
    /// <see cref="Duration"/> and a <see cref="Period"/> on it are rows of the ability or the actor applying it.
    /// </summary>
    public string? DurationPolicy { get; init; }

    /// <summary>
    /// The <c>EMaxHealthModificationHealthPatchPolicy</c> member the effect's <c>CRAttributeModComponent</c> states for
    /// Health when the effect changes MaxHealth. Null where the component is absent or leaves it at the class default.
    /// </summary>
    public string? MaxHealthModificationPolicy { get; init; }

    /// <summary>
    /// The <c>EMaxHealthModificationHealthReductionPolicy</c> member the effect's <c>CRAttributeModComponent</c> states
    /// for Health when the effect's removal lowers MaxHealth. Null where the component is absent or leaves it at the
    /// class default.
    /// </summary>
    public string? MaxHealthOnRemovedPolicy { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    /// <summary>Attribute-based modifiers its executions apply to the attributes they capture.</summary>
    public IReadOnlyList<CalculationModifier>? CalculationModifiers { get; init; }

    /// <summary>Modifiers its executions apply with a magnitude the applier sets on the spec.</summary>
    public IReadOnlyList<SetByCallerModifier>? SetByCallerModifiers { get; init; }

    /// <summary>
    /// The row of <c>DT_EffectCalculationPresets</c> the effect's <c>EffectCalculationPreset</c> names, on an effect
    /// with a damage or healing execution. <c>settings.json</c>'s <c>effectCalculationPresets</c> has the row.
    /// </summary>
    public string? CalculationPreset { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyList<Threshold>? Thresholds { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public EntityReference? NameBorrowedFrom { get; init; }
    public EntityReference? TextureBorrowedFrom { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public EntityReference? AppliedBy { get; init; }

    /// <summary>
    /// The effect's <c>VisualDisplayName</c>, a member of <c>CRGameplayEffectDamage</c>: the name of a combat log line
    /// with the effect's id, on an effect with no <c>GameplayEffectUIData_BuffDebuff</c> name.
    /// </summary>
    public string? VisualDisplayName { get; init; }

    /// <summary>
    /// The effect's <c>AlwaysUseVisualDisplayName</c>, a member of <c>CRGameplayEffectDamage</c>. With a
    /// <see cref="VisualDisplayName"/> it gives the effect's hit a combat log line of its own id and name.
    /// </summary>
    public bool? AlwaysUseVisualDisplayName { get; init; }

    /// <summary>
    /// The member of <c>CRCombatLogAbilityEffectData</c> with the id of the combat log line of the effect's hit, on an
    /// effect with a damage or healing execution. <c>EffectClass</c>: the line has the effect's id. The effect derives
    /// from <c>CRGameplayEffectDamage</c> and has a <see cref="DurationPolicy"/> other than <c>Instant</c>, or has
    /// <see cref="AlwaysUseVisualDisplayName"/> with a <see cref="VisualDisplayName"/>. <c>AbilityClass</c>: the line
    /// has the id of the hit's instigating ability, or of that ability's <c>logAsADifferentAbility</c>. Where the
    /// ability has no <c>CRAbilityDataRow</c> name, or the hit has no ability, the line has the effect's id. An
    /// <see cref="AlwaysUseVisualDisplayName"/> effect of an auto attack ability is logged as <c>AbilityClass</c>.
    /// </summary>
    public string? LogIdClass { get; init; }

    /// <summary>
    /// The effect or ability with the id of the combat log line of the effect's hit, with the ability in
    /// <see cref="AppliedBy"/> as the hit's instigating ability. An <see cref="AppliedBy"/> effect has its own
    /// instigating ability. Null for a <see cref="LogIdClass"/> of <c>AbilityClass</c> with no applying ability.
    /// </summary>
    public EntityReference? LoggedUnder { get; init; }

    /// <summary>Effects applying this one through a linked effect component.</summary>
    public IReadOnlyList<EffectLink>? LinkedBy { get; init; }

    /// <summary>The commit waits applying the effect.</summary>
    public IReadOnlyList<CommitTrigger>? AppliedOnCommitOf { get; init; }

    public EntityReference? PartOf { get; init; }

    /// <summary>The effect whose Blueprint class this effect's class derives from.</summary>
    public EntityReference? ParentClass { get; init; }

    /// <summary>Effects the effect's properties and graph reference.</summary>
    public IReadOnlyList<EntityReference>? Applies { get; init; }

    /// <summary>Effects applied by or part of the same ability as this one.</summary>
    public IReadOnlyList<EntityReference>? Siblings { get; init; }

    /// <summary>Abilities the effect grants.</summary>
    public IReadOnlyList<EntityReference>? Grants { get; init; }

    public IReadOnlyList<EntityReference>? Origins { get; init; }

    public string? Role { get; init; }

    public IReadOnlyList<EntityReference>? ChangedBy { get; init; }

    public IReadOnlyList<Change>? Changes { get; init; }
}
