namespace Fellowship.Codex.Models;

public static class Distance
{
    public const string Unit = "yd";

    public const decimal UnitsPerYard = 100m;

    public const double MeleeReach = 250;

    public static bool Melee(double units) => units <= MeleeReach;

    public static decimal Yards(double units) =>
        Math.Round((decimal)units / UnitsPerYard, MidpointRounding.AwayFromZero);
}
