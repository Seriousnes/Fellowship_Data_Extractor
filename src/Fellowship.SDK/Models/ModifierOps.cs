using System.Collections.Frozen;

namespace Fellowship.SDK.Models;

public static class ModifierOps
{
    private const string Division = "Division";
    private const string Subtract = "Subtract";

    private static readonly FrozenSet<string> Factors = FrozenSet.Create(StringComparer.Ordinal,
        "Multiplicitive", "MultiplyCompound", Division, "Multiply", "CooldownMultipliers");

    private static readonly FrozenSet<string> Terms = FrozenSet.Create(StringComparer.Ordinal,
        "Additive", "AddFinal", "Add", Subtract, "CooldownPreAdditives", "CooldownPostAdditives");

    /// <summary>
    /// True when applying <paramref name="value"/> under <paramref name="op"/> leaves the attribute as it was:
    /// a factor of 1, or a term of 0.
    /// </summary>
    public static bool Unchanged(string? op, double value) =>
        op is not null && (Factors.Contains(op) ? value == 1 : Terms.Contains(op) && value == 0);

    /// <summary>True when <paramref name="op"/> adds its magnitude to the attribute, or subtracts it.</summary>
    public static bool Adds(string? op) => op is not null && Terms.Contains(op);

    /// <summary>True when <paramref name="op"/> multiplies the attribute by a factor, or divides it.</summary>
    public static bool Multiplies(string? op) => op is not null && Factors.Contains(op);

    /// <summary>The factor <paramref name="op"/> multiplies the attribute by: the reciprocal for a division.</summary>
    public static double Factor(string op, double value) => op == Division && value != 0 ? 1 / value : value;

    /// <summary>The term <paramref name="op"/> adds to the attribute: the negation for a subtraction.</summary>
    public static double Term(string op, double value) => op == Subtract ? -value : value;
}
