namespace Fellowship.Codex.Models;

public static class StatConversion
{
    public const string PercentageSuffix = "Percentage";

    public static bool Converts(AttributeDisplay attribute) =>
        attribute.BaseStatMultiplier != 1 || attribute.Brackets is { Count: > 0 };

    private static bool IsFraction(AttributeDisplay attribute) => attribute.BaseStatMultiplier != 1;

    public static decimal FromRating(decimal rating, AttributeDisplay attribute)
    {
        if (!Converts(attribute)) return rating;

        return Scaled(Diminished(rating * (decimal)attribute.BaseStatMultiplier, attribute), attribute);
    }

    public static decimal FromPercentage(decimal percentage, AttributeDisplay attribute) =>
        Scaled(percentage, attribute);

    private static decimal Scaled(decimal percentage, AttributeDisplay attribute) =>
        IsFraction(attribute) ? percentage / 100m : percentage;

    private static decimal Diminished(decimal raw, AttributeDisplay attribute)
    {
        if (attribute.Brackets is not { Count: > 0 } brackets) return raw;

        var total = 0m;
        var floor = 0m;

        foreach (var bracket in brackets)
        {
            var ceiling = (decimal)bracket.MaxBracketValue;
            if (ceiling <= floor) continue;

            var inside = Math.Min(raw, ceiling) - floor;
            if (inside <= 0) return total;

            total += inside * (decimal)bracket.PenaltyPercentage;
            floor = ceiling;
        }

        if (raw > floor) total += (raw - floor) * (decimal)attribute.FinalPenaltyPercentage;

        return total;
    }
}
