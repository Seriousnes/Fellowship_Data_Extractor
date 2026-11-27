namespace Fellowship.SDK.Models;

/// <summary>
/// A modifier an effect's execution applies for that execution alone, with the magnitude the applier sets on the spec
/// under <see cref="DataTag"/>. <see cref="Op"/> applies it to <see cref="Attribute"/> or to the
/// <see cref="Transient"/> aggregator. Path to Oblivion (Burst) divides <c>DamageScale</c> by
/// <c>SetByCaller.DamageScale</c>, the number of characters its soak circle overlaps.
/// </summary>
/// <param name="Execution">The execution calculation class, such as <c>CRIntellectDamageCalculation</c>.</param>
/// <param name="Op">The modifier op, such as <c>Division</c>.</param>
/// <param name="DataTag">The set-by-caller tag of the magnitude, such as <c>SetByCaller.DamageScale</c>.</param>
public sealed record SetByCallerModifier(string Execution, string Op, string DataTag)
{
    /// <summary>The captured attribute the modifier applies to.</summary>
    public string? Attribute { get; init; }

    /// <summary>The transient aggregator tag the modifier applies to.</summary>
    public string? Transient { get; init; }

    /// <summary><c>Source</c> or <c>Target</c>: whose <see cref="Attribute"/> is captured.</summary>
    public string? AttributeSource { get; init; }
}
