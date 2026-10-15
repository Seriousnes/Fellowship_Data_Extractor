using Fellowship.SDK.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TooltipQueryTests(Export export)
{
    private static IQueryCollection Raw(params (string Key, string[] Values)[] values) =>
        new QueryCollection(values.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Values)));

    private static IReadOnlySet<string> Declared => RequestProperties.OfEveryTooltip;

    private TooltipQuery Query(ItemTooltipRequest request, out string? error) =>
        TooltipRequests.Read(request, export.Store, out error);

    private static TooltipQuery Query(BlessingTooltipRequest request, out string? error) =>
        TooltipRequests.Read(request, out error);

    private IReadOnlyList<string> Rolled(ItemTooltipRequest request, out string? error) =>
        Query(request, out error).RandomStats;

    private string Flat => export.Store.Stats.Names
        .First(name => export.Store.Stats.Resolve(name) is { } stat &&
            !StatConversion.Converts(export.Store.Stats.Row(stat)));

    [Fact]
    public void An_item_reads_the_armor_the_caller_states()
    {
        var query = Query(new ItemTooltipRequest { Armor = 303m }, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
    }

    [Fact]
    public void An_item_reads_each_imbued_trait_and_the_rank_it_sits_at()
    {
        var query = Query(
            new ItemTooltipRequest
            {
                ImbuedTraits = [new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)],
                HasImbuedTraits = true,
            },
            out _);

        Assert.Equal([new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)], query.ImbuedTraits);
        Assert.True(query.HasImbuedTraits);
    }

    [Fact]
    public void An_item_reads_every_modifier_the_caller_states_in_the_order_stated()
    {
        ItemModifier[] modifiers =
        [
            new(ModifierKind.Trait, 412, 1),
            new(ModifierKind.Blessing, 7, 1),
            new(ModifierKind.Essence, 3, 100),
        ];

        Assert.Equal(modifiers, Query(new ItemTooltipRequest { Modifiers = modifiers }, out _).Modifiers);
    }

    [Fact]
    public void An_item_carries_the_allocated_count_the_caller_states_beside_the_list()
    {
        var query = Query(
            new ItemTooltipRequest
            {
                ImbuedTraits = [new ImbuedTrait(12, 2), new ImbuedTrait(7, 1)],
                AllocatedTraits = 4,
            },
            out _);

        Assert.Equal(2, query.ImbuedTraits.Count);
        Assert.Equal(4, query.AllocatedTraits);
    }

    [Fact]
    public void An_item_counts_no_traits_when_the_caller_names_none()
    {
        var query = Query(new ItemTooltipRequest(), out _);

        Assert.Null(query.AllocatedTraits);
        Assert.Null(query.HasImbuedTraits);
        Assert.Empty(query.ImbuedTraits);
        Assert.Empty(query.Modifiers);
        Assert.False(query.NoSocket);
    }

    [Fact]
    public void An_item_keeps_armor_for_the_printed_line_and_still_reads_a_stat()
    {
        var query = Query(
            new ItemTooltipRequest { Armor = 303m, Stats = Stats.Rated("stamina", 36m) }, out var error);

        Assert.Null(error);
        Assert.Equal(303m, query.Armor);
        Assert.Equal(36m, query.Total("Stamina"));
    }

    [Fact]
    public void A_stat_name_the_build_does_not_carry_is_refused()
    {
        var query = Query(new ItemTooltipRequest { Stats = Stats.Rated("nonesuch", 1m) }, out var error);

        Assert.Equal("Unknown stat 'nonesuch'.", error);
        Assert.Empty(query.Stats);
    }

    [Fact]
    public void Two_rolls_of_one_stat_stay_two_magnitudes_of_their_own()
    {
        var query = Query(new ItemTooltipRequest { Stats = Stats.Rated("Haste", 100m, 250m) }, out var error);

        Assert.Null(error);
        Assert.Equal([100m, 250m], query.Stats["Haste"]);
        Assert.Equal(350m, query.Total("Haste"));
    }

    [Fact]
    public void A_percentage_roll_on_a_stat_that_converts_nothing_is_refused()
    {
        var query = Query(new ItemTooltipRequest { Stats = Stats.Percent(Flat, 10m) }, out var error);

        Assert.Equal($"'{Flat}' is not a rating, so its percentage rolls have nothing to convert.", error);
        Assert.Empty(query.Attributes);
    }

    [Fact]
    public void A_magnitude_past_the_ceiling_a_coefficient_can_be_applied_to_is_refused()
    {
        var query = Query(
            new ItemTooltipRequest { Stats = Stats.Rated("Haste", 1_000_000_000_001m) }, out var error);

        Assert.NotNull(error);
        Assert.Contains("past the", error);
        Assert.Empty(query.Stats);
    }

    [Fact]
    public void The_page_reads_past_a_parameter_and_refuses_a_key_that_is_neither_that_nor_a_stat()
    {
        Assert.Empty(StatQuery.FromQuery(Raw(("armor", ["303"])), Declared, export.Store.Stats, out var known));
        Assert.Null(known);

        StatQuery.FromQuery(Raw(("nonesuch", ["1"])), Declared, export.Store.Stats, out var error);

        Assert.Equal("Unknown parameter 'nonesuch'.", error);
    }

    [Fact]
    public void The_page_reads_a_percentage_suffix_as_the_percentage_roll_of_the_stat_it_names()
    {
        var stats = StatQuery.FromQuery(
            Raw(($"Haste{StatConversion.PercentageSuffix}", ["10"])),
            Declared,
            export.Store.Stats,
            out var error);

        Assert.Null(error);
        Assert.Equal([10m], stats["Haste"].Percentage);
        Assert.Null(stats["Haste"].Rating);
    }

    [Fact]
    public void The_page_keeps_a_repeated_key_as_a_roll_of_its_own()
    {
        var stats = StatQuery.FromQuery(
            Raw(("Haste", ["100", "250"])), Declared, export.Store.Stats, out var error);

        Assert.Null(error);
        Assert.Equal([100m, 250m], stats["Haste"].Rating);
    }

    [Fact]
    public void An_item_reads_each_random_stat_the_caller_names_once_per_roll()
    {
        var rolled = Rolled(
            new ItemTooltipRequest { RandomStats = ["Stamina", "Expertise", "Expertise"] }, out var error);

        Assert.Null(error);
        Assert.Equal(["Stamina", "Expertise", "Expertise"], rolled);
    }

    [Fact]
    public void An_item_reads_a_random_stat_by_the_name_the_game_prints()
    {
        var rolled = Rolled(
            new ItemTooltipRequest { RandomStats = ["Critical Strike", "CritChance"] }, out var error);

        Assert.Null(error);
        Assert.Equal(["CritChance", "CritChance"], rolled);
    }

    [Fact]
    public void An_item_refuses_a_random_stat_no_slot_rolls()
    {
        Rolled(new ItemTooltipRequest { RandomStats = ["Stamina", "Armor"] }, out var error);

        Assert.NotNull(error);
        Assert.Contains("'Armor' is not a stat a random slot rolls.", error);
        Assert.Contains("Stamina", error);
    }

    [Fact]
    public void An_item_rolls_nothing_when_the_caller_names_nothing() =>
        Assert.Empty(Rolled(new ItemTooltipRequest(), out _));

    [Fact]
    public void A_request_naming_only_the_hero_carries_the_hero_and_nothing_more()
    {
        var query = Query(new ItemTooltipRequest { Hero = "Barbarian" }, out var error);

        Assert.Null(error);
        Assert.Equal("Barbarian", query.Hero);
        Assert.Null(query.Rarity);
        Assert.Null(query.ItemLevel);
    }

    [Fact]
    public void A_blank_hero_reads_as_none_given() =>
        Assert.Null(StatQuery.NullIfEmpty(new ItemTooltipRequest { Hero = string.Empty }.Hero));

    [Fact]
    public void The_state_and_the_equipped_tier_a_blessing_is_drawn_at_reach_the_query()
    {
        var query = Query(new BlessingTooltipRequest { State = "allocated", EquippedTier = 2 }, out _);

        Assert.Equal("allocated", query.State);
        Assert.Equal(2, query.EquippedTier);
    }

    [Fact]
    public void A_request_naming_only_a_stat_carries_the_stat_and_nothing_else()
    {
        var query = Query(new ItemTooltipRequest { Stats = Stats.Rated("Stamina", 40m) }, out var error);

        Assert.Null(error);
        Assert.Equal(40m, query.Total("Stamina"));
        Assert.Null(query.Hero);
    }
}
