using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Tests;

public sealed class StatConversionTests
{
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

    [Fact]
    public void FiftyRatingIsEightPercent() =>
        Assert.Equal(0.08m, StatConversion.FromRating(50m, Rated()));

    [Fact]
    public void TwoHundredRatingCrossesEveryBracket()
    {
        Assert.Equal(0.3084m, StatConversion.FromRating(200m, Rated()));
        Assert.NotEqual(0.32m, StatConversion.FromRating(200m, Rated()));
    }

    [Theory]
    [InlineData(62.5, 0.10)]
    [InlineData(93.75, 0.149)]
    [InlineData(125, 0.197)]
    [InlineData(156.25, 0.244)]
    public void BandCeilingsBelongToTheBandBelow(double rating, double expected) =>
        Assert.Equal((decimal)expected, StatConversion.FromRating((decimal)rating, Rated()));

    [Fact]
    public void NothingConvertsToNothing() => Assert.Equal(0m, StatConversion.FromRating(0m, Rated()));

    [Fact]
    public void IdentityStatsPassThrough()
    {
        Assert.False(StatConversion.Converts(Identity()));
        Assert.Equal(250m, StatConversion.FromRating(250m, Identity()));
    }

    [Fact]
    public void PercentageFormDoesNotDiminishAgain()
    {
        Assert.Equal(0.08m, StatConversion.FromPercentage(8m, Rated()));
        Assert.Equal(
            StatConversion.FromRating(200m, Rated()),
            StatConversion.FromPercentage(30.84m, Rated()));
    }

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

        Assert.Equal(0.15m, StatConversion.FromRating(40m, repeated));
    }

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

        Assert.Equal(45m, StatConversion.FromRating(50m, armor));
        Assert.Equal(85m, StatConversion.FromRating(100m, armor));
        Assert.Equal(95m, StatConversion.FromRating(110m, armor));
    }
}
