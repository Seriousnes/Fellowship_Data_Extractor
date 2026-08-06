using FellowshipExtractor.Api.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Tests;

public sealed class StatParameterTests
{
    private static StatParameters Parameters() =>
        new(["Haste", "Strength", "CritChance"], Export.Loaded.Vocabulary.StatCalculation.Attributes);

    private static TooltipQuery Parse(out string? error, params (string Key, string Value)[] pairs) =>
        TooltipQuery.Parse(
            new QueryCollection(pairs
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => new StringValues(group.Select(p => p.Value).ToArray()))),
            Parameters(),
            out error);

    [Fact]
    public void ASlotKeepsTheNumberTheCallerWrote()
    {
        var query = Parse(out var error, ("haste", "50"));

        Assert.Null(error);
        Assert.Equal([50m], query.Stats["Haste"]);
        Assert.Equal(0.08m, query.Haste);
    }

    [Fact]
    public void ThePercentageFormSaysTheSameThing()
    {
        var query = Parse(out var error, ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.Equal(0.08m, query.Haste);
    }

    [Fact]
    public void ThePercentageFormClaimsNoSlot()
    {
        var query = Parse(out var error, ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.False(query.Stats.ContainsKey("Haste"));
    }

    [Theory]
    [InlineData("Critical Strike", "50")]
    [InlineData("CriticalStrike", "50")]
    [InlineData("Critical Strike Percentage", "8")]
    [InlineData("CriticalStrikePercentage", "8")]
    public void APrintedNameResolvesInEitherForm(string key, string value)
    {
        var query = Parse(out var error, (key, value));

        Assert.Null(error);
        Assert.Equal(0.08m, query.Attribute("CritChance"));
    }

    [Fact]
    public void BothSpellingsAccumulate()
    {
        var query = Parse(out var error, ("haste", "50"), ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.Equal(0.16m, query.Haste);
    }

    [Fact]
    public void TheLadderIsClimbedOnceForTheWholeStat()
    {
        var query = Parse(out var error, ("haste", "50"), ("haste", "50"));

        Assert.Null(error);
        Assert.Equal([50m, 50m], query.Stats["Haste"]);
        Assert.Equal(0.1586m, query.Haste);
    }

    [Fact]
    public void APrimaryStatPassesThrough()
    {
        var query = Parse(out var error, ("strength", "250"));

        Assert.Null(error);
        Assert.Equal(250m, query.Total("Strength"));
    }

    [Fact]
    public void APrimaryStatHasNoPercentageForm()
    {
        Parse(out var error, ("strengthPercentage", "8"));

        Assert.NotNull(error);
        Assert.Contains("'strength'", error);
        Assert.Contains("'strengthPercentage'", error);
    }

    [Theory]
    [InlineData("spirt")]
    [InlineData("spirtPercentage")]
    public void AMisspellingIsRejected(string key)
    {
        Parse(out var error, (key, "17"));

        Assert.Equal($"Unknown parameter '{key}'.", error);
    }

    [Theory]
    [InlineData("CritChance")]
    [InlineData("CritChancePercentage")]
    public void AnInternalSpellingResolvesNowhere(string key)
    {
        Parse(out var error, (key, "50"));

        Assert.Equal($"Unknown parameter '{key}'.", error);
    }
}
