using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TooltipQueryTests(Export export)
{
    private static IQueryCollection Raw(params (string Key, string[] Values)[] asked) =>
        new QueryCollection(asked.ToDictionary(a => a.Key, a => new StringValues(a.Values)));

    private IReadOnlyList<string> Rollable => export.Store.Settings.StatCalculation.RandomStatPool;

    [Fact]
    public void An_item_reads_the_armor_the_caller_states()
    {
        var query = new ItemTooltipRequest { Id = 1, Armor = 303m }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
    }

    [Fact]
    public void An_item_reads_each_imbued_trait_and_the_rank_it_sits_at()
    {
        Assert.True(ImbuedTraits.TryParse("12:2,7", out var traits));

        var query = new ItemTooltipRequest { Id = 1, ImbuedTraits = traits, HasImbuedTraits = true }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out _);

        Assert.Equal([new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)], query.ImbuedTraits);
        Assert.True(query.HasImbuedTraits);
    }

    [Fact]
    public void An_item_reads_every_modifier_the_caller_states_in_the_order_stated()
    {
        Assert.True(ItemModifiers.TryParse("trait:412:1,blessing:7:1,essence:3:100", out var modifiers));

        var query = new ItemTooltipRequest { Id = 1, Modifiers = modifiers }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out _);

        Assert.Equal(modifiers.Values, query.Modifiers);
    }

    [Fact]
    public void An_item_carries_the_allocated_count_the_caller_states_beside_the_list()
    {
        Assert.True(ImbuedTraits.TryParse("12:2,7", out var traits));

        var query = new ItemTooltipRequest { Id = 1, ImbuedTraits = traits, AllocatedTraits = 4 }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out _);

        Assert.Equal(2, query.ImbuedTraits.Count);
        Assert.Equal(4, query.AllocatedTraits);
    }

    [Fact]
    public void An_item_counts_no_traits_when_the_caller_names_none()
    {
        var query = new ItemTooltipRequest { Id = 1 }.ToQuery(Raw(), export.Store.Stats, Rollable, out _);

        Assert.Null(query.AllocatedTraits);
        Assert.Null(query.HasImbuedTraits);
        Assert.Empty(query.ImbuedTraits);
        Assert.Empty(query.Modifiers);
        Assert.False(query.NoSocket);
    }

    [Fact]
    public void An_item_keeps_armor_for_the_printed_line_and_still_reads_a_stat()
    {
        var query = new ItemTooltipRequest { Id = 1, Armor = 303m }
            .ToQuery(Raw(("stamina", ["36"])), export.Store.Stats, Rollable, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
        Assert.Equal(36m, query.Total("Stamina"));
    }

    [Fact]
    public void An_item_refuses_a_key_that_is_neither_its_property_nor_a_stat()
    {
        new ItemTooltipRequest { Id = 1 }
            .ToQuery(Raw(("nonesuch", ["1"])), export.Store.Stats, Rollable, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void An_item_reads_each_random_stat_the_caller_names_once_per_roll()
    {
        Assert.True(RolledStats.TryParse("Stamina,Expertise,Expertise", out var rolled));

        var query = new ItemTooltipRequest { Id = 1, RandomStats = rolled }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out var error);

        Assert.Null(error);
        Assert.Equal(["Stamina", "Expertise", "Expertise"], query.RandomStats);
    }

    [Fact]
    public void An_item_reads_a_random_stat_by_the_name_the_game_prints()
    {
        Assert.True(RolledStats.TryParse("Critical Strike,CritChance", out var rolled));

        var query = new ItemTooltipRequest { Id = 1, RandomStats = rolled }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out var error);

        Assert.Null(error);
        Assert.Equal(["CritChance", "CritChance"], query.RandomStats);
    }

    [Fact]
    public void An_item_refuses_a_random_stat_no_slot_rolls()
    {
        Assert.True(RolledStats.TryParse("Stamina,Armor", out var rolled));

        new ItemTooltipRequest { Id = 1, RandomStats = rolled }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out var error);

        Assert.NotNull(error);
        Assert.Contains("'Armor' is not a stat a random slot rolls.", error);
        Assert.Contains("Stamina", error);
    }

    [Fact]
    public void An_item_rolls_nothing_when_the_caller_names_nothing() =>
        Assert.Empty(new ItemTooltipRequest { Id = 1 }
            .ToQuery(Raw(), export.Store.Stats, Rollable, out _).RandomStats);

    [Fact]
    public void An_ability_carries_the_hero_and_nothing_more()
    {
        var query = new AbilityTooltipRequest { Id = 1, Hero = "Barbarian" }
            .ToQuery(Raw(), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal("Barbarian", query.Hero);
        Assert.Null(query.Rarity);
        Assert.Null(query.ItemLevel);
    }

    [Fact]
    public void An_ability_reads_a_blank_hero_as_none_given()
    {
        var query = new AbilityTooltipRequest { Id = 1, Hero = string.Empty }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Null(query.Hero);
    }

    [Fact]
    public void A_blessing_carries_the_state_and_the_tier()
    {
        var query = new BlessingTooltipRequest { Id = 1, State = "allocated", EquippedTier = 2 }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal("allocated", query.State);
        Assert.Equal(2, query.EquippedTier);
    }

    [Fact]
    public void A_trait_carries_the_state()
    {
        var query = new TraitTooltipRequest { Id = 1, State = "unavailable" }
            .ToQuery(Raw(), export.Store.Stats, out _);

        Assert.Equal("unavailable", query.State);
    }

    [Fact]
    public void A_gem_and_a_currency_carry_the_rung()
    {
        Assert.Equal(
            "3",
            new GemTooltipRequest { Id = 1, Rarity = "3" }.ToQuery(Raw(), export.Store.Stats, out _).Rarity);

        Assert.Equal(
            "3",
            new CurrencyTooltipRequest { Id = 1, Rarity = "3" }.ToQuery(Raw(), export.Store.Stats, out _).Rarity);
    }

    [Fact]
    public void A_bare_type_carries_the_stats_and_nothing_else()
    {
        var query = new EffectTooltipRequest { Id = 1 }
            .ToQuery(Raw(("Stamina", ["40"])), export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
        Assert.Null(query.Hero);
    }
}
