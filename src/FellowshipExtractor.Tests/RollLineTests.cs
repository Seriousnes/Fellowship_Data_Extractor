using FellowshipExtractor.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Tests;

public sealed class RollLineTests
{
    private const int FrostedCloak = 2665;

    private static StatParameters Parameters() =>
        new(["Stamina", "Agility", "Expertise", "Spirit"], Export.Loaded.Vocabulary.StatCalculation.Attributes);

    private static IReadOnlyList<StatLine> Lines(params (string Key, string Value)[] pairs)
    {
        var query = TooltipQuery.Parse(
            new QueryCollection(pairs
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => new StringValues(group.Select(p => p.Value).ToArray()))),
            Parameters(),
            out var error);

        Assert.Null(error);

        return StatMagnitudes.For(
            Export.Loaded.Entity("ItemID", FrostedCloak),
            Export.Loaded.Vocabulary.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            StatMagnitudes.MaximumItemLevel,
            query);
    }

    private static decimal Line(IReadOnlyList<StatLine> lines, string stat) =>
        lines.Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase)).Value;

    [Fact]
    public void ARatedSlotPrintsTheRatingTheCallerSent() =>
        Assert.Equal(17m, Line(Lines(("spirit", "17")), "Spirit"));

    [Fact]
    public void APrimarySlotPrintsTheNumberTheCallerSent() =>
        Assert.Equal(250m, Line(Lines(("agility", "250")), "Agility"));

    [Fact]
    public void AnUnnamedSlotIsStillComputed() =>
        Assert.True(Line(Lines(("spirit", "17")), "Expertise") > 0m);

    [Fact]
    public void ThePercentageFormLeavesTheSlotComputed()
    {
        var computed = Line(Lines(), "Spirit");

        Assert.Equal(computed, Line(Lines(("spiritPercentage", "2.72")), "Spirit"));
        Assert.NotEqual(2.72m, computed);
    }
}
