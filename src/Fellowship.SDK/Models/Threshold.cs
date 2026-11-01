namespace Fellowship.SDK.Models;

/// <summary>
/// Attribute bound by a threshold: a Blueprint comparison, an attribute-change wait, or a native character state an
/// effect or ability requires.
/// </summary>
public sealed record Threshold(string Attribute, double Value)
{
    /// <summary>
    /// Attribute <see cref="Value"/> is a fraction of, such as MaxHealth. Absent when the value is the attribute itself.
    /// </summary>
    public string? Maximum { get; init; }

    /// <summary>
    /// Comparison of the attribute against <see cref="Value"/>, named as <c>EWaitAttributeChangeComparison</c> names its
    /// members. Absent for a native state, whose comparison is in native code.
    /// </summary>
    public string? Comparison { get; init; }

    /// <summary>
    /// Native character state the value bounds, such as LowHealth or Healthy. The value is the state's
    /// <c>FractionTheshold</c> on <c>CRCharacterBase</c>.
    /// </summary>
    public string? State { get; init; }

    /// <summary>True when the condition is the state's absence.</summary>
    public bool? Absent { get; init; }

    /// <summary>True when the state is in the target's tag requirements, so the target's attribute is compared.</summary>
    public bool? Target { get; init; }
}
