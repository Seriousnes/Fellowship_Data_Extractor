using System.Collections.Frozen;

namespace Fellowship.SDK.Models;

public static class ModifierOps
{
    private static readonly FrozenSet<string> Factors = FrozenSet.Create(StringComparer.Ordinal,
        "Multiplicitive", "MultiplyCompound", "Division", "Multiply", "CooldownMultipliers");

    private static readonly FrozenSet<string> Terms = FrozenSet.Create(StringComparer.Ordinal,
        "Additive", "AddFinal", "Add", "Subtract", "CooldownPreAdditives", "CooldownPostAdditives");

    /// <summary>
    /// True when applying <paramref name="value"/> under <paramref name="op"/> leaves the attribute as it was:
    /// a factor of 1, or a term of 0.
    /// </summary>
    public static bool Unchanged(string? op, double value) =>
        op is not null && (Factors.Contains(op) ? value == 1 : Terms.Contains(op) && value == 0);
}
