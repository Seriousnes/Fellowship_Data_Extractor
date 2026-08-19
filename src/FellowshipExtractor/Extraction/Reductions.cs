using CUE4Parse.MappingsProvider;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class Reductions
{
    public const string Format = "reduction";

    public static IReadOnlySet<string> MultiplyingOps(TypeMappings mappings)
    {
        var members = EnumOrdinals.Members(mappings, GameplayEffects.OpEnum).Values
            .ToHashSet(StringComparer.Ordinal);

        var missing = GameplayEffects.MultiplyingOps.Where(op => !members.Contains(op)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{GameplayEffects.OpEnum} in the mappings declares no {string.Join(" and no ", missing)}, so a " +
                "modifier that multiplies cannot be told from one that adds. Read the enum out of " +
                $"mappings/{ExtractorOptions.MappingsPattern} and name the members this build ships.");
        }

        return members.Intersect(GameplayEffects.MultiplyingOps, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool Prints(
        Placeholder placeholder, IReadOnlyList<EffectModifier> modifiers, IReadOnlySet<string> multiplying)
    {
        if (placeholder.Format is not null) return false;
        if (placeholder.Percent is not true) return false;
        if (placeholder.Coefficient is not null) return false;
        if (placeholder.Value is not { } value || value >= 1) return false;

        if (placeholder.Multiplies is true) return true;

        return modifiers.Any(modifier =>
            modifier.Op is { } op && multiplying.Contains(op) && modifier.Value == value);
    }
}
