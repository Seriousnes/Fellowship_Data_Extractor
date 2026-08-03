using FellowshipExtractor.Api;

namespace FellowshipExtractor.Tests;

/// <summary>
/// What DT_AttributeProperties still says. These are the drift guards: each one names a number the
/// build shipped when the conversion was written, so a balance patch that moves one fails here
/// rather than quietly changing every tooltip the API renders.
///
/// Failing is the point. A test here going red means the game changed, not that the code broke -
/// read the new value, decide whether it is intended, and move the expectation.
/// </summary>
public sealed class AttributeTableTests
{
    private static readonly string[] RatedStats = ["Haste", "CritChance", "Expertise", "Spirit"];

    private static AttributeDisplayView Attribute(string stat)
    {
        var attributes = Export.Loaded.Vocabulary.StatCalculation.Attributes;

        Assert.True(attributes.ContainsKey(stat), $"DT_AttributeProperties no longer has a '{stat}' row.");
        return new AttributeDisplayView(attributes[stat]);
    }

    /// <summary>The conversion the four rated stats share: a rating is worth 0.16% of itself.</summary>
    [Theory]
    [InlineData("Haste")]
    [InlineData("CritChance")]
    [InlineData("Expertise")]
    [InlineData("Spirit")]
    public void RatedStatsConvertAtSixteenHundredths(string stat) =>
        Assert.Equal(0.16, Attribute(stat).Value.BaseStatMultiplier);

    /// <summary>
    /// The diminishing returns ladder, band by band. Four bands at 10, 15, 20 and 25 keeping the
    /// whole, then 98%, 96% and 94%, with 92% above the last.
    /// </summary>
    [Theory]
    [InlineData("Haste")]
    [InlineData("CritChance")]
    [InlineData("Expertise")]
    [InlineData("Spirit")]
    public void RatedStatsShareTheSameLadder(string stat)
    {
        var attribute = Attribute(stat).Value;
        var brackets = attribute.Brackets;

        Assert.NotNull(brackets);
        Assert.Equal(4, brackets!.Count);

        Assert.Equal([10d, 15d, 20d, 25d], brackets.Select(b => b.MaxBracketValue));
        Assert.Equal([1d, 0.98d, 0.96d, 0.94d], brackets.Select(b => b.PenaltyPercentage));
        Assert.Equal(0.92, attribute.FinalPenaltyPercentage);
    }

    /// <summary>
    /// Each rated stat names a rating attribute its own row is not keyed by. That mismatch is the
    /// whole reason the conversion exists: gear rolls HasteRating and gameplay effects capture
    /// Haste, and they are different quantities.
    /// </summary>
    [Theory]
    [InlineData("Haste", "HasteRating")]
    [InlineData("CritChance", "CritRating")]
    [InlineData("Expertise", "ExpertiseRating")]
    [InlineData("Spirit", "SpiritRating")]
    public void RatedStatsNameARatingAttribute(string stat, string expected)
    {
        Assert.Equal(expected, Attribute(stat).Value.Attribute);
        Assert.NotEqual(stat, Attribute(stat).Value.Attribute);
    }

    /// <summary>
    /// The primary stats are the number they say. Every coefficient in the build multiplies one of
    /// these, so if one ever gained a multiplier or a band, damage numbers would move.
    /// </summary>
    [Theory]
    [InlineData("Strength")]
    [InlineData("Agility")]
    [InlineData("Intellect")]
    [InlineData("Stamina")]
    public void PrimaryStatsAreNotRatings(string stat)
    {
        var attribute = Attribute(stat).Value;

        Assert.Equal(1, attribute.BaseStatMultiplier);
        Assert.Null(attribute.Brackets);
        Assert.False(StatConversion.Converts(attribute));
    }

    /// <summary>
    /// The invariant that makes the conversion safe to apply at all: every attribute a resolved
    /// coefficient multiplies is one the conversion leaves alone. If a future build gave one of
    /// them a multiplier, or pointed a coefficient at a rated stat, every damage number carrying it
    /// would silently change scale.
    /// </summary>
    [Fact]
    public void NoCoefficientMultipliesAStatThatConverts()
    {
        var attributes = Export.Loaded.Vocabulary.StatCalculation.Attributes;

        var multiplied = Export.Loaded.All()
            .SelectMany(e => e.Placeholders?.Values ?? [])
            .Where(p => p.Coefficient is not null)
            .Select(p => p.Attribute)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(multiplied);

        var converting = multiplied
            .Where(a => attributes.TryGetValue(a, out var declared) && StatConversion.Converts(declared))
            .ToArray();

        Assert.True(
            converting.Length == 0,
            $"A coefficient now multiplies {string.Join(", ", converting)}, which the conversion rescales. " +
            "Every damage number using it has changed scale.");
    }

    /// <summary>
    /// The whole table is still there. A row vanishing would take its display name with it and the
    /// tooltip would start printing the internal stat name at the reader.
    /// </summary>
    [Fact]
    public void EveryStatTheApiAcceptsHasARow()
    {
        var attributes = Export.Loaded.Vocabulary.StatCalculation.Attributes;

        foreach (var stat in RatedStats.Concat(["Strength", "Agility", "Intellect", "Stamina", "Armor"]))
        {
            Assert.True(attributes.ContainsKey(stat), $"No DT_AttributeProperties row for '{stat}'.");
            Assert.False(
                string.IsNullOrWhiteSpace(attributes[stat].DisplayName),
                $"'{stat}' has no display name, so a tooltip would print the internal name.");
        }
    }

    /// <summary>CritChance prints as the words the game uses, which is not its internal name.</summary>
    [Fact]
    public void CritChancePrintsAsCriticalStrike() =>
        Assert.Equal("Critical Strike", Attribute("CritChance").Value.DisplayName);

    /// <summary>
    /// The CooldownReduction row states three of its four bands at the same ceiling, so two of them
    /// hold nothing and the game cannot reach them either. Recorded rather than repaired: the
    /// extractor reproduces what shipped.
    ///
    /// This is here to notice the day it is fixed. If the ceilings ever ascend, the conversion for
    /// that stat starts giving different numbers and this test says why.
    /// </summary>
    [Fact]
    public void CooldownReductionStillRepeatsItsBandCeilings()
    {
        var brackets = Attribute("CooldownReduction").Value.Brackets;

        Assert.NotNull(brackets);

        var ceilings = brackets!.Select(b => b.MaxBracketValue).ToArray();
        var ascending = ceilings.Zip(ceilings.Skip(1), (a, b) => b > a).All(rising => rising);

        Assert.False(
            ascending,
            $"CooldownReduction's band ceilings now ascend ({string.Join(", ", ceilings)}), so bands that " +
            "never applied before now do. The numbers that stat converts to have changed.");
    }

    /// <summary>A named holder, so a failure names the stat rather than an index into a dictionary.</summary>
    private readonly record struct AttributeDisplayView(Tooltip.AttributeDisplay Value);
}
