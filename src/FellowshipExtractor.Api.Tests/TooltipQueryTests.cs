using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipQueryTests(Export export)
{
    private TooltipQuery Parse(out string? error, params (string Key, string[] Values)[] asked) =>
        TooltipQuery.Parse(
            new QueryCollection(asked.ToDictionary(a => a.Key, a => new StringValues(a.Values))),
            export.Store.Stats,
            out error);

    [Fact]
    public void Reads_the_armor_the_caller_states()
    {
        var query = Parse(out var error, ("armor", ["303"]));

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
    }

    [Fact]
    public void Says_what_is_wrong_with_an_armor_that_is_not_a_number()
    {
        Parse(out var error, ("armor", ["heavy"]));

        Assert.Equal("armor 'heavy' is not a number.", error);
    }

    [Fact]
    public void Reads_each_imbued_trait_and_the_rank_it_sits_at()
    {
        var query = Parse(out var error, ("trait", ["12:2", "7"]), ("maxTraits", ["5"]));

        Assert.Null(error);
        Assert.Equal([new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)], query.Traits);
        Assert.Equal(5, query.MaxTraits);
    }

    [Fact]
    public void Counts_the_traits_it_was_given_when_the_caller_states_no_count()
    {
        var counted = Parse(out _, ("trait", ["12:2,7:1"]));
        var stated = Parse(out _, ("trait", ["12:2,7:1"]), ("allocatedTraits", ["5"]));

        Assert.Equal(2, counted.AllocatedTraits);
        Assert.Equal(5, stated.AllocatedTraits);
        Assert.Null(Parse(out _).AllocatedTraits);
    }

    [Fact]
    public void Says_what_is_wrong_with_a_trait_that_names_no_id()
    {
        Parse(out var error, ("trait", ["hunters-focus"]));

        Assert.Equal(
            "trait entry 'hunters-focus' is not an item trait id, optionally followed by a colon and the rank " +
            "it sits at.",
            error);
    }

    [Fact]
    public void Leaves_armor_free_for_the_reserved_parameter_to_claim()
    {
        var stat = export.Store.Stats.Resolve("armor");

        Assert.True(
            stat is null,
            $"'armor' is reserved for the armour line, and it now also spells the stat {stat}. Reserved keys " +
            "are read first, so that stat can no longer be sent. Both Armor and BonusArmor print the word " +
            "Armor, so either becoming a declared slot stat trips this.");
    }

    [Fact]
    public void Keeps_reading_a_stat_that_shares_its_spelling_with_nothing_reserved()
    {
        var query = Parse(out var error, ("stamina", ["36"]), ("armor", ["303"]));

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
        Assert.Equal(36m, query.Total("Stamina"));
    }
}
