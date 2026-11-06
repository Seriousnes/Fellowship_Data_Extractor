using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class EffectDocument : EntityDocument
{
    public string? Description { get; init; }
    public PrimaryStat? PrimaryStat { get; init; }
    public string? Icon { get; init; }

    public IReadOnlyList<string>? EffectTags { get; init; }

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
    /// declares it does not execute then.
    /// </summary>
    public int? Ticks { get; init; }

    public bool? Infinite { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    /// <summary>Attribute-based modifiers its executions apply to the attributes they capture.</summary>
    public IReadOnlyList<CalculationModifier>? CalculationModifiers { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyList<Threshold>? Thresholds { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public EntityReference? NameBorrowedFrom { get; init; }
    public EntityReference? TextureBorrowedFrom { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public EntityReference? AppliedBy { get; init; }

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
