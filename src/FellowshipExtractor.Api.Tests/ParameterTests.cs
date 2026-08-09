using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ParameterTests(Export export)
{
    private static IQueryCollection Query(params (string Key, string[] Values)[] asked) =>
        new QueryCollection(asked.ToDictionary(a => a.Key, a => new StringValues(a.Values)));

    private sealed record Probe
    {
        public int Id { get; init; }

        public string? Hero { get; init; }
    }

    [Fact]
    public void A_request_model_declares_its_own_property_names()
    {
        var declared = RequestProperties.Of(typeof(Probe));

        Assert.Equal(["Hero", "Id"], declared.Order(StringComparer.Ordinal));
        Assert.Contains("hero", declared);
    }

    [Fact]
    public void A_key_that_is_neither_a_property_nor_a_stat_is_named_back()
    {
        StatQuery.Read(new Probe(), Query(("nonesuch", ["1"])), export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void A_stat_the_build_names_is_read_off_the_raw_query()
    {
        var query = StatQuery.Read(new Probe(), Query(("Stamina", ["40"])), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
    }

    [Fact]
    public void A_property_of_the_model_is_left_for_the_binder()
    {
        StatQuery.Read(new Probe(), Query(("hero", ["Barbarian"])), export.Store.Stats, out var error);

        Assert.Null(error);
    }

    [Fact]
    public void A_comma_and_a_repetition_read_the_same_traits()
    {
        Assert.True(ImbuedTraits.TryParse("1:2,3:4", out var joined));
        Assert.True(ImbuedTraits.TryParse(string.Join(',', "1:2", "3:4"), out var repeated));

        Assert.Equal([new ImbuedTrait(1, 2), new ImbuedTrait(3, 4)], joined.Values);
        Assert.Equal(joined.Values, repeated.Values);

        Assert.True(ImbuedTraits.TryParse("7", out var bare));
        Assert.Equal([new ImbuedTrait(7, 1)], bare.Values);

        Assert.False(ImbuedTraits.TryParse("hunters-focus", out _));
    }

    [Fact]
    public void A_comma_and_a_repetition_read_the_same_gem_powers()
    {
        Assert.True(GemPowers.TryParse("1:2,3:4", out var joined));

        Assert.Equal([new GemPower(1, 2), new GemPower(3, 4)], joined.Values);
        Assert.False(GemPowers.TryParse("1", out _));
    }
}
