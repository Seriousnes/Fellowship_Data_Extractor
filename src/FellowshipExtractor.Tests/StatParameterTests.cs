using FellowshipExtractor.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Tests;

/// <summary>
/// How a stat parameter is read: which spellings address a stat, which of them says the caller has
/// already converted the number, and what a stat is worth once it has been.
///
/// Two rules meet here and neither one implies the other. A stat is addressed by the name the game
/// prints, so the build's own spelling resolves nowhere; and a stat that is a rating arrives as the
/// attribute it feeds, so the number the query carries is not the number the caller wrote. A change
/// to either would leave the other compiling and answering silently wrong tooltips, which is what
/// these pin.
/// </summary>
public sealed class StatParameterTests
{
    private static StatParameters Parameters() =>
        new(["Haste", "Strength", "CritChance"], Export.Loaded.Vocabulary.StatCalculation.Attributes);

    /// <summary>
    /// The pairs as a query string carries them: a key repeated in the URL is one key holding
    /// several values, which is how a caller writes an item with two slots of one stat.
    /// </summary>
    private static TooltipQuery Parse(out string? error, params (string Key, string Value)[] pairs) =>
        TooltipQuery.Parse(
            new QueryCollection(pairs
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => new StringValues(group.Select(p => p.Value).ToArray()))),
            Parameters(),
            out error);

    /// <summary>
    /// A slot keeps the number the caller wrote. 50 haste rating stays 50, because that is what the
    /// item's line prints, and the 8% it is worth is a second reading of the same parameter rather
    /// than a replacement for it.
    /// </summary>
    [Fact]
    public void ASlotKeepsTheNumberTheCallerWrote()
    {
        var query = Parse(out var error, ("haste", "50"));

        Assert.Null(error);
        Assert.Equal([50m], query.Stats["Haste"]);
        Assert.Equal(0.08m, query.Haste);
    }

    /// <summary>The same stat, for a caller reading the percentage off their own character sheet.</summary>
    [Fact]
    public void ThePercentageFormSaysTheSameThing()
    {
        var query = Parse(out var error, ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.Equal(0.08m, query.Haste);
    }

    /// <summary>
    /// And states the wearer rather than the item, so it claims no slot. An item asked about with it
    /// computes its own lines from its level, which is what it does for a caller who named no stat.
    /// </summary>
    [Fact]
    public void ThePercentageFormClaimsNoSlot()
    {
        var query = Parse(out var error, ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.False(query.Stats.ContainsKey("Haste"));
    }

    /// <summary>
    /// A printed name is two words for one of the eight stats, and whitespace is ignored on the way
    /// in, so the percentage form has to survive the same compaction the plain form does.
    /// </summary>
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

    /// <summary>Both spellings of one stat land under the stat, so a reader asking its total gets both.</summary>
    [Fact]
    public void BothSpellingsAccumulate()
    {
        var query = Parse(out var error, ("haste", "50"), ("hastePercentage", "8"));

        Assert.Null(error);
        Assert.Equal(0.16m, query.Haste);
    }

    /// <summary>
    /// Two slots of one rating are one ladder, not two. 50 and 50 is 100 rating, worth 15.86% rather
    /// than the 16% converting each slot on its own would give, because the diminishing returns are
    /// taken once against everything the character holds.
    /// </summary>
    [Fact]
    public void TheLadderIsClimbedOnceForTheWholeStat()
    {
        var query = Parse(out var error, ("haste", "50"), ("haste", "50"));

        Assert.Null(error);
        Assert.Equal([50m, 50m], query.Stats["Haste"]);
        Assert.Equal(0.1586m, query.Haste);
    }

    /// <summary>A stat that is not a rating is the number it says, which is what keeps coefficients still.</summary>
    [Fact]
    public void APrimaryStatPassesThrough()
    {
        var query = Parse(out var error, ("strength", "250"));

        Assert.Null(error);
        Assert.Equal(250m, query.Total("Strength"));
    }

    /// <summary>
    /// And has no percentage to be given, so the form is turned away rather than quietly answering a
    /// question the build does not ask. The sentence names the caller's spelling, because the
    /// build's is not an address this accepts.
    /// </summary>
    [Fact]
    public void APrimaryStatHasNoPercentageForm()
    {
        Parse(out var error, ("strengthPercentage", "8"));

        Assert.NotNull(error);
        Assert.Contains("'strength'", error);
        Assert.Contains("'strengthPercentage'", error);
    }

    /// <summary>
    /// A misspelling is still rejected rather than ignored, with or without the suffix, because a
    /// caller who types spirt=17 would otherwise get a tooltip printed from a stat of zero.
    /// </summary>
    [Theory]
    [InlineData("spirt")]
    [InlineData("spirtPercentage")]
    public void AMisspellingIsRejected(string key)
    {
        Parse(out var error, (key, "17"));

        Assert.Equal($"Unknown parameter '{key}'.", error);
    }

    /// <summary>
    /// The build's own spelling still resolves nowhere. Registering it beside the printed name is
    /// what made two addresses answer differently for one stat, and the percentage form must not
    /// reopen it.
    /// </summary>
    [Theory]
    [InlineData("CritChance")]
    [InlineData("CritChancePercentage")]
    public void AnInternalSpellingResolvesNowhere(string key)
    {
        Parse(out var error, (key, "50"));

        Assert.Equal($"Unknown parameter '{key}'.", error);
    }
}
