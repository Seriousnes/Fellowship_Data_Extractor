using System.Text.Json;
using FellowshipExtractor.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Tests;

/// <summary>
/// What an item's stat lines read when the caller describes the copy they hold.
///
/// A caller who names a stat is describing the slot their item rolled, so the line prints the number
/// they wrote. The four rated stats are where that is easy to lose: they are also the ones an
/// ability's arithmetic reads as a converted attribute, and a conversion applied on the way in would
/// leave a rolled 17 printing as 0.0272 with nothing failing to say so.
/// </summary>
public sealed class RollLineTests
{
    /// <summary>Frosted Cloak, whose four slots include both a rated stat and a primary one.</summary>
    private const int FrostedCloak = 2665;

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// The item as the magnitudes are worked out from it: the equipment position it is worn in and
    /// the weighted slots it declares, which with no rarity in play is everything the arithmetic
    /// reads. One id is an item and a gameplay effect both, so the type is what picks the item.
    /// </summary>
    private static Entity Cloak()
    {
        var body = Export.Loaded.Documents
            .Where(document => document.Type == Fellowship.SDK.EntityType.Item && document.Id == FrostedCloak)
            .Select(document => document.Body)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Item {FrostedCloak} is not in the export. Either the id moved or the entity is gone.");

        return new Entity
        {
            Slot = body["slot"]?.GetValue<string>(),
            Stats = body["stats"].Deserialize<IReadOnlyList<StatSlot>>(Reading),
        };
    }

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
            Cloak(),
            Export.Loaded.Vocabulary.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            StatMagnitudes.MaximumItemLevel,
            query);
    }

    private static decimal Line(IReadOnlyList<StatLine> lines, string stat) =>
        lines.Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>A rated slot the caller named prints the rating they named it with.</summary>
    [Fact]
    public void ARatedSlotPrintsTheRatingTheCallerSent() =>
        Assert.Equal(17m, Line(Lines(("spirit", "17")), "Spirit"));

    /// <summary>So does a primary one, which never converted either way.</summary>
    [Fact]
    public void APrimarySlotPrintsTheNumberTheCallerSent() =>
        Assert.Equal(250m, Line(Lines(("agility", "250")), "Agility"));

    /// <summary>
    /// A slot the caller said nothing about is still computed from the item's own level, so naming
    /// one stat does not empty the others.
    /// </summary>
    [Fact]
    public void AnUnnamedSlotIsStillComputed() =>
        Assert.True(Line(Lines(("spirit", "17")), "Expertise") > 0m);

    /// <summary>
    /// The percentage form describes the wearer and not this copy, so it takes no slot over: the
    /// item's own spirit line stays the one its level is worth.
    /// </summary>
    [Fact]
    public void ThePercentageFormLeavesTheSlotComputed()
    {
        var computed = Line(Lines(), "Spirit");

        Assert.Equal(computed, Line(Lines(("spiritPercentage", "2.72")), "Spirit"));
        Assert.NotEqual(2.72m, computed);
    }
}
