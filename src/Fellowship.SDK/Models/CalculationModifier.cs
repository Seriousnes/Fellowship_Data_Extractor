namespace Fellowship.SDK.Models;

/// <summary>
/// An attribute-based modifier an effect's execution applies for that execution alone. The modifier's magnitude is
/// <c>Coefficient × (BackingAttribute + PreMultiplyAdditiveValue) + PostMultiplyAdditiveValue</c>, and
/// <see cref="Op"/> applies it to <see cref="Attribute"/> or to the <see cref="Transient"/> aggregator. Creeping Death
/// on Seething Poison multiplies <c>DamageScale</c> by <c>0.5 × (Haste − 1) + 1</c>.
/// </summary>
/// <param name="Execution">The execution calculation class, such as <c>CRAgilityDamageCalculation</c>.</param>
/// <param name="Op">The modifier op, such as <c>MultiplyCompound</c>.</param>
/// <param name="BackingAttribute">The attribute the magnitude reads.</param>
public sealed record CalculationModifier(string Execution, string Op, string BackingAttribute)
{
    /// <summary>The captured attribute the modifier applies to.</summary>
    public string? Attribute { get; init; }

    /// <summary>The transient aggregator tag the modifier applies to, such as <c>Transient.Heal</c>.</summary>
    public string? Transient { get; init; }

    /// <summary><c>Source</c> or <c>Target</c>: whose <see cref="BackingAttribute"/> the magnitude reads.</summary>
    public string? BackingSource { get; init; }

    /// <summary>True when the backing attribute is captured once at application rather than read at each execution.</summary>
    public bool? BackingSnapshot { get; init; }

    /// <summary>The tuning row or curve table row the coefficient reads, absent for a literal.</summary>
    public string? Row { get; init; }

    /// <summary>The coefficient, or its scale where <see cref="CoefficientCurve"/> varies with level.</summary>
    public double Coefficient { get; init; }

    /// <summary>The curve table row the coefficient is multiplied by, where the row's value varies with level.</summary>
    public Curve? CoefficientCurve { get; init; }

    public double PreMultiplyAdditiveValue { get; init; }

    public double PostMultiplyAdditiveValue { get; init; }
}
