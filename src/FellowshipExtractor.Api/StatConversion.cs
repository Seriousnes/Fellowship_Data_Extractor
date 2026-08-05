namespace FellowshipExtractor.Api;

/// <summary>
/// Turning a rating a caller holds into the attribute it feeds.
///
/// A gear slot rolls a rating and the thing the game acts on is a percentage. DT_AttributeProperties
/// states the whole conversion per stat: BaseStatMultiplier takes the rating to a raw percentage,
/// then the brackets take their cut of it band by band, and FinalPenaltyPercentage applies to
/// whatever is left above the last band. 50 haste rating is 8%, and 200 is not 32% but 30.84%:
/// the whole of the first 10, then 4.9 of the next 5, 4.8 of the 5 after, 4.7 of the 5 after that,
/// and 0.92 of each of the remaining 7.
///
/// The arithmetic here is the build's. The one reading that is not is the scale of the answer: a
/// stat whose multiplier is one is not a rating at all - Strength is the number it says - so it
/// passes through, while a stat that does convert lands as the fraction the build's own constants
/// use, because Hero.Spirit.Buff.HasteIncrease is 0.3 for the +30% its ability advertises.
///
/// Armor sits between the two: multiplier one, so it keeps its scale, but it does declare brackets
/// and they are applied. Nothing in the export multiplies a coefficient by Armor, so the reading is
/// untested by any shipped tooltip and is flagged rather than trusted.
/// </summary>
public static class StatConversion
{
    /// <summary>The suffix a caller uses to say they have already done the conversion themselves.</summary>
    public const string PercentageSuffix = "Percentage";

    /// <summary>
    /// Whether this attribute is a rating that becomes something else. A multiplier of one with no
    /// brackets is the identity, and saying so keeps the primary stats out of the arithmetic
    /// entirely rather than running them through a conversion that cannot change them.
    /// </summary>
    public static bool Converts(AttributeDisplay attribute) =>
        attribute.BaseStatMultiplier != 1 || attribute.Brackets is { Count: > 0 };

    /// <summary>Whether the converted value is a fraction rather than a count.</summary>
    private static bool IsFraction(AttributeDisplay attribute) => attribute.BaseStatMultiplier != 1;

    /// <summary>The attribute value a rating is worth.</summary>
    public static decimal FromRating(decimal rating, AttributeDisplay attribute)
    {
        if (!Converts(attribute)) return rating;

        return Scaled(Diminished(rating * (decimal)attribute.BaseStatMultiplier, attribute), attribute);
    }

    /// <summary>
    /// The attribute value a percentage the caller worked out themselves is worth. The brackets are
    /// not applied again: a caller writing the percentage form is saying this is the number after
    /// the game has taken its cut, which is what a player reads off their own character sheet.
    /// </summary>
    public static decimal FromPercentage(decimal percentage, AttributeDisplay attribute) =>
        Scaled(percentage, attribute);

    private static decimal Scaled(decimal percentage, AttributeDisplay attribute) =>
        IsFraction(attribute) ? percentage / 100m : percentage;

    /// <summary>
    /// The raw percentage with each band's cut taken out of the part of it that falls in that band.
    ///
    /// The bands are read from the bottom up in the order the row states them, and a band whose
    /// ceiling is at or below the one before it can never contain anything. That is not defensive
    /// coding for its own sake: the shipped CooldownReduction row states three bands at a ceiling of
    /// 1000, so two of them are unreachable in the game as well, and this reproduces that rather
    /// than repairing it.
    /// </summary>
    private static decimal Diminished(decimal raw, AttributeDisplay attribute)
    {
        if (attribute.Brackets is not { Count: > 0 } brackets) return raw;

        var kept = 0m;
        var floor = 0m;

        foreach (var bracket in brackets)
        {
            var ceiling = (decimal)bracket.MaxBracketValue;
            if (ceiling <= floor) continue;

            var inside = Math.Min(raw, ceiling) - floor;
            if (inside <= 0) return kept;

            kept += inside * (decimal)bracket.PenaltyPercentage;
            floor = ceiling;
        }

        // Everything above the last band the row reaches.
        if (raw > floor) kept += (raw - floor) * (decimal)attribute.FinalPenaltyPercentage;

        return kept;
    }
}
