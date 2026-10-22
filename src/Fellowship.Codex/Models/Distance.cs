namespace Fellowship.Codex.Models;

public static class Distance
{
    public static bool Self(double units) => units <= 0;

    public static bool Melee(double units, AbilityTooltipText text) => units <= text.MeleeMaximum;

    public static decimal Yards(double units, AbilityTooltipText text) =>
        Math.Round((decimal)units / (decimal)text.UnitsPerYard, MidpointRounding.AwayFromZero);
}
