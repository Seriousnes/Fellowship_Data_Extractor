namespace Fellowship.Codex.Models;

public static class Distance
{
    public const string Unit = "yd";

    public const decimal CentimetresPerYard = 91.44m;

    public static decimal Yards(double units) =>
        Math.Round((decimal)units / CentimetresPerYard, MidpointRounding.AwayFromZero);
}
