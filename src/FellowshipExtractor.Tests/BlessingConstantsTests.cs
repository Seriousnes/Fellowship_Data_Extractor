using System.Globalization;
using System.Text.RegularExpressions;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed partial class BlessingConstantsTests
{
    private const int TheMonarch = 705;

    private const string MonarchRows = "Finesse.MajorIncreasedCooldownRecoveryPerHaste.";
    private const string CooldownAcceleration = MonarchRows + "CooldownAcceleration";
    private const string HasteThreshold = MonarchRows + "HasteThreshold";
    private const string BonusIncreasePerHaste = MonarchRows + "BonusIncreasePerHasteInPercentage";

    private static IReadOnlyList<GlyphDocument> Blessings =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<GlyphDocument>()];

    private static GlyphDocument Monarch => Blessings.Single(blessing => blessing.Id == TheMonarch);

    [GeneratedRegex(@"(\d+(?:\.\d+)?)% / (\d+(?:\.\d+)?)% / (\d+(?:\.\d+)?)% / (\d+(?:\.\d+)?)% Cooldown Acceleration")]
    private static partial Regex PrintedAcceleration { get; }

    [GeneratedRegex(@"up to a maximum of (\d+(?:\.\d+)?)%")]
    private static partial Regex PrintedMaximum { get; }

    [Fact]
    public void EveryBlessingConstantHasAValueAtEachRank()
    {
        Assert.NotEmpty(Blessings);

        foreach (var blessing in Blessings)
        {
            Assert.NotEmpty(blessing.Constants!);
            Assert.All(blessing.Constants!.Values, ranks => Assert.Equal(blessing.MaxRank, ranks.Count));
        }
    }

    [Theory]
    [InlineData(CooldownAcceleration)]
    [InlineData(HasteThreshold)]
    [InlineData(BonusIncreasePerHaste)]
    public void TheMonarchStatesEachRowOfItsFormula(string row) => Assert.Contains(row, Monarch.Constants!.Keys);

    [Fact]
    public void TheMonarchsCooldownAccelerationMatchesItsPrintedRanks()
    {
        var printed = PrintedAcceleration.Match(Monarch.Description!);
        Assert.True(printed.Success);

        var ranks = Monarch.Constants![CooldownAcceleration];

        for (var rank = 0; rank < ranks.Count; rank++)
        {
            Assert.Equal(Percent(printed.Groups[rank + 1].Value), ranks[rank]!.Value * 100, 9);
        }
    }

    [Fact]
    public void TheMonarchsHasteTermReachesItsPrintedMaximum()
    {
        var printed = PrintedMaximum.Match(Monarch.Description!);
        Assert.True(printed.Success);

        var thresholds = Monarch.Constants![HasteThreshold];
        var bonuses = Monarch.Constants![BonusIncreasePerHaste];

        for (var rank = 0; rank < thresholds.Count; rank++)
        {
            Assert.Equal(Percent(printed.Groups[1].Value), thresholds[rank]!.Value * bonuses[rank]!.Value * 100, 9);
        }
    }

    private static double Percent(string printed) => double.Parse(printed, CultureInfo.InvariantCulture);
}
