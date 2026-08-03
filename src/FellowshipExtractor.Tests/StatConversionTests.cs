using FellowshipExtractor.Api;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The rating-to-percentage arithmetic, against tables written here rather than read from the
/// build. These say the code computes what it claims; <see cref="AttributeTableTests"/> says the
/// build still hands it the numbers it was written for. Both have to hold and neither implies the
/// other, which is why they are apart.
/// </summary>
public sealed class StatConversionTests
{
    /// <summary>The shape the four rated stats share, as of the build these tests were written for.</summary>
    private static AttributeDisplay Rated(double multiplier = 0.16) => new(
        DisplayName: "Haste",
        RoundToDecimals: 0,
        IsPercent: false,
        Attribute: "HasteRating",
        BaseStatMultiplier: multiplier,
        Brackets:
        [
            new RatingBracket(10, 1),
            new RatingBracket(15, 0.98),
            new RatingBracket(20, 0.96),
            new RatingBracket(25, 0.94),
        ],
        FinalPenaltyPercentage: 0.92);

    private static AttributeDisplay Identity() => new(
        DisplayName: "Strength", RoundToDecimals: 0, IsPercent: false, Attribute: "Strength");

    /// <summary>
    /// The conversion the user stated independently of the build: 50 haste rating is 8%. It stays
    /// inside the first band, so it is the multiplier alone and pins that end of the arithmetic.
    /// </summary>
    [Fact]
    public void FiftyRatingIsEightPercent() =>
        Assert.Equal(0.08m, StatConversion.FromRating(50m, Rated()));

    /// <summary>
    /// 200 rating crosses every band and lands in the final penalty, so this is the one case that
    /// exercises the whole ladder: 10 kept whole, then 4.9, 4.8 and 4.7 of the three five-wide
    /// bands, then 0.92 of the remaining 7. Not 32%, which is what no diminishing returns would give.
    /// </summary>
    [Fact]
    public void TwoHundredRatingCrossesEveryBracket()
    {
        Assert.Equal(0.3084m, StatConversion.FromRating(200m, Rated()));
        Assert.NotEqual(0.32m, StatConversion.FromRating(200m, Rated()));
    }

    /// <summary>
    /// A raw percentage landing exactly on a band's ceiling belongs to that band and not the next.
    /// 62.5 rating is exactly 10%, the first ceiling, and is kept whole.
    /// </summary>
    [Theory]
    [InlineData(62.5, 0.10)]     // exactly 10 raw: the whole of the first band
    [InlineData(93.75, 0.149)]   // exactly 15 raw: 10 + 5 x 0.98
    [InlineData(125, 0.197)]     // exactly 20 raw: + 5 x 0.96
    [InlineData(156.25, 0.244)]  // exactly 25 raw: + 5 x 0.94, the last band's ceiling
    public void BandCeilingsBelongToTheBandBelow(double rating, double expected) =>
        Assert.Equal((decimal)expected, StatConversion.FromRating((decimal)rating, Rated()));

    [Fact]
    public void NothingConvertsToNothing() => Assert.Equal(0m, StatConversion.FromRating(0m, Rated()));

    /// <summary>
    /// A stat with no bands and a multiplier of one is not a rating, and converting it changes it
    /// into nothing else. This is what keeps every damage coefficient in the build unaffected.
    /// </summary>
    [Fact]
    public void IdentityStatsPassThrough()
    {
        Assert.False(StatConversion.Converts(Identity()));
        Assert.Equal(250m, StatConversion.FromRating(250m, Identity()));
    }

    /// <summary>
    /// The percentage form is the number after the game has taken its cut, so the bands are not
    /// applied to it a second time. A caller writing 30.84 gets what 200 rating gets.
    /// </summary>
    [Fact]
    public void PercentageFormDoesNotDiminishAgain()
    {
        Assert.Equal(0.08m, StatConversion.FromPercentage(8m, Rated()));
        Assert.Equal(
            StatConversion.FromRating(200m, Rated()),
            StatConversion.FromPercentage(30.84m, Rated()));
    }

    /// <summary>
    /// Rating and percentage agree across the range, which is the property that lets a caller use
    /// whichever they hold. Checked at points either side of every band boundary.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(1000)]
    public void RatingAndPercentageAgree(double rating)
    {
        var attribute = Rated();
        var fromRating = StatConversion.FromRating((decimal)rating, attribute);
        var percent = fromRating * 100m;

        Assert.Equal(fromRating, StatConversion.FromPercentage(percent, attribute));
    }

    /// <summary>
    /// More rating is never less attribute, all the way past the last band. A penalty read as a
    /// replacement rather than a share of the band would break this and nothing else would notice.
    /// </summary>
    [Fact]
    public void ConversionRisesWithRating()
    {
        var attribute = Rated();
        var previous = -1m;

        for (var rating = 0m; rating <= 2000m; rating += 7m)
        {
            var converted = StatConversion.FromRating(rating, attribute);
            Assert.True(converted > previous, $"{rating} rating gave {converted}, no more than {previous}.");
            previous = converted;
        }
    }

    /// <summary>
    /// A band whose ceiling does not clear the one before it holds nothing, so it neither
    /// contributes nor swallows what follows. The build ships exactly this in the CooldownReduction
    /// row, where three bands sit at 1000, and the game cannot reach the later ones either.
    /// </summary>
    [Fact]
    public void BandsThatCannotBeReachedAreSkipped()
    {
        var repeated = new AttributeDisplay(
            DisplayName: "Repeated",
            RoundToDecimals: 0,
            IsPercent: true,
            Attribute: "RepeatedRating",
            BaseStatMultiplier: 0.5,
            Brackets: [new RatingBracket(10, 1), new RatingBracket(10, 0.5), new RatingBracket(10, 0.25)],
            FinalPenaltyPercentage: 0.5);

        // 40 rating is 20 raw. The first band takes its 10 whole, the two behind it sit at the same
        // ceiling and can hold nothing, and the final penalty takes half of the remaining 10.
        Assert.Equal(0.15m, StatConversion.FromRating(40m, repeated));
    }

    /// <summary>
    /// A stat with bands but a multiplier of one keeps its own scale. Armor is the shipped case and
    /// nothing in the export multiplies a coefficient by it, so this pins the reading rather than
    /// claiming the game agrees with it.
    /// </summary>
    [Fact]
    public void BandedStatsWithoutAMultiplierKeepTheirScale()
    {
        var armor = new AttributeDisplay(
            DisplayName: "Armor",
            RoundToDecimals: 0,
            IsPercent: false,
            Attribute: "Armor",
            BaseStatMultiplier: 1,
            Brackets: [new RatingBracket(50, 0.9), new RatingBracket(100, 0.8)],
            FinalPenaltyPercentage: 1);

        Assert.True(StatConversion.Converts(armor));

        // 50 x 0.9 = 45, then 50 x 0.8 = 40, then the rest at 1.
        Assert.Equal(45m, StatConversion.FromRating(50m, armor));
        Assert.Equal(85m, StatConversion.FromRating(100m, armor));
        Assert.Equal(95m, StatConversion.FromRating(110m, armor));
    }
}
