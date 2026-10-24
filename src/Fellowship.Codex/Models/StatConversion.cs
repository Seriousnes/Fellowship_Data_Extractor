namespace Fellowship.Codex.Models;

/// <summary>
/// The rating to percentage conversion of <c>UCRAttributeRatingToPercentageModifier</c>, as the shipping binary of build
/// 26162532 computes it at 0x1453A51D0: each bracket's penalty multiplies the whole remaining raw percentage before that
/// bracket's band is filled, so the penalties compound and each band is a band of the result.
/// </summary>
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

        var remaining = raw;
        var total = 0m;
        var previousCeiling = 0m;

        if (remaining > 0)
        {
            foreach (var bracket in brackets)
            {
                remaining *= (decimal)bracket.PenaltyPercentage;

                var inside = Math.Min((decimal)bracket.MaxBracketValue - previousCeiling, remaining);
                previousCeiling = (decimal)bracket.MaxBracketValue;
                remaining -= inside;
                total += inside;

                if (remaining <= 0) break;
            }
        }

        return remaining * (decimal)attribute.FinalPenaltyPercentage + total;
    }
}
