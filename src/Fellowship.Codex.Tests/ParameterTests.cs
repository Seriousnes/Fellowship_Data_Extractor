using Fellowship.SDK.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ParameterTests(Export export)
{
    private static IQueryCollection Query(params (string Key, string[] Values)[] pairs) =>
        new QueryCollection(pairs.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Values)));

    private sealed record Probe
    {
        public int Id { get; init; }

        public string? Hero { get; init; }
    }

    private static IReadOnlySet<string> Declared => RequestProperties.Of(typeof(Probe));

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
        StatQuery.FromQuery(Query(("nonesuch", ["1"])), Declared, export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void A_stat_the_build_names_is_read_off_the_raw_query()
    {
        var stats = StatQuery.FromQuery(Query(("Stamina", ["40"])), Declared, export.Store.Stats, out var error);

        Assert.Null(error);

        var query = StatQuery.Read(stats, export.Store.Stats, out error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
    }

    [Fact]
    public void A_property_of_the_model_is_left_for_the_binder()
    {
        StatQuery.FromQuery(Query(("hero", ["Barbarian"])), Declared, export.Store.Stats, out var error);

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
    public void One_modifier_and_several_read_back_as_the_entries_they_name()
    {
        Assert.True(ItemModifiers.TryParse("trait:412:1", out var alone));

        Assert.Equal([new ItemModifier(ModifierKind.Trait, 412, 1)], alone.Values);

        Assert.True(ItemModifiers.TryParse("trait:412:1,blessing:7:1,essence:3:100", out var several));

        Assert.Equal(
            [
                new ItemModifier(ModifierKind.Trait, 412, 1),
                new ItemModifier(ModifierKind.Blessing, 7, 1),
                new ItemModifier(ModifierKind.Essence, 3, 100),
            ],
            several.Values);
    }

    [Fact]
    public void Every_kind_the_enum_names_is_read_from_a_token_of_its_own()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            Assert.True(ItemModifiers.TryParse($"{TooltipView.Token(kind)}:1:2", out var read));
            Assert.Equal([new ItemModifier(kind, 1, 2)], read.Values);

            Assert.True(ItemModifiers.TryParse($"{TooltipView.Token(kind).ToUpperInvariant()}:1:2", out var loud));
            Assert.Equal(read.Values, loud.Values);
        }
    }

    [Fact]
    public void An_entry_the_reader_cannot_make_out_is_refused()
    {
        Assert.False(ItemModifiers.TryParse("nonesuch:1:2", out _));
        Assert.False(ItemModifiers.TryParse("trait:1", out _));
        Assert.False(ItemModifiers.TryParse("trait", out _));
        Assert.False(ItemModifiers.TryParse("trait:one:2", out _));
        Assert.False(ItemModifiers.TryParse("trait:1:two", out _));
        Assert.False(ItemModifiers.TryParse("trait:1:2:3", out _));
        Assert.False(ItemModifiers.TryParse("trait:412:1,nonesuch:7:1", out _));

        Assert.True(ItemModifiers.TryParse(null, out var none));
        Assert.Empty(none.Values);
    }
}
