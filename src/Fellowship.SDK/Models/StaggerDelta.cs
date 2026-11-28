namespace Fellowship.SDK.Models;

/// <summary>
/// The <c>StaggerDelta</c> an ability's Blueprint assigns, under the set-by-caller tag of <c>CRModifyStaggerExecution</c>,
/// to a spec of <see cref="Effect"/>. The execution adds it to the target's Stagger unscaled, so a negative delta is a
/// cleanse. Amend Fate assigns
/// <c>min(StaggerRemovalIntCoefficient × Source.Intellect × (Source.Expertise + 1) × RandomFloatInRange(StaggerRemovalSpreadMin, StaggerRemovalSpreadMax), Target.GetStagger) × -1</c>.
/// </summary>
public sealed record StaggerDelta
{
    /// <summary>The effect whose spec has the magnitude.</summary>
    public EntityReference? Effect { get; init; }

    /// <summary>The set-by-caller tag the magnitude is assigned to.</summary>
    public string DataTag { get; init; } = string.Empty;

    /// <summary><see cref="Expression"/> in one line. A row is named by its last segment.</summary>
    public string Formula { get; init; } = string.Empty;

    public MagnitudeTerm Expression { get; init; } = new();
}

/// <summary>Entity-side <see cref="StaggerDelta"/>, with the effect as the tag record it resolves from.</summary>
public sealed record StaggerDeltaSource(IconSource? Effect, string DataTag, string Formula, MagnitudeTerm Expression);

/// <summary>
/// One node of a magnitude a Blueprint computes. A node is an operation over <see cref="Operands"/>, a
/// <see cref="Constant"/>, a constants <see cref="Row"/>, an <see cref="Attribute"/>, or a <see cref="Read"/> of a
/// native getter or an event field.
/// </summary>
public sealed record MagnitudeTerm
{
    /// <summary>
    /// <c>Multiply</c>, <c>Add</c>, <c>Subtract</c>, <c>Divide</c>, <c>Min</c>, <c>Max</c> or
    /// <c>RandomFloatInRange</c>. <c>Min</c> and <c>Max</c> are a <c>SelectFloat</c> over a comparison of its own two
    /// values.
    /// </summary>
    public string? Op { get; init; }

    public IReadOnlyList<MagnitudeTerm>? Operands { get; init; }

    public double? Constant { get; init; }

    /// <summary>The constants row a scalable float property reads.</summary>
    public string? Row { get; init; }

    /// <summary>The value of <see cref="Row"/>, times the property's own <c>Value</c>.</summary>
    public double? Value { get; init; }

    /// <summary>The attribute <c>GetFloatAttribute</c> reads off <see cref="Of"/>.</summary>
    public string? Attribute { get; init; }

    /// <summary>A native getter called on <see cref="Of"/>, such as <c>GetStagger</c>, or a field of an event.</summary>
    public string? Read { get; init; }

    /// <summary>
    /// <c>Source</c> for the ability's owner, <c>Target</c> for a character of the target data, or the event a field
    /// is read from, such as <c>HealthChangedEvent</c>.
    /// </summary>
    public string? Of { get; init; }
}
