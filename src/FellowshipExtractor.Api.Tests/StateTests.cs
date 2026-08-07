using FellowshipExtractor.Api.Components.Tooltip;

using Microsoft.AspNetCore.Http;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StateTests : BunitContext
{
    private readonly Export export;

    public StateTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
    }

    private TraitDocument Trait => export.Store.Entities.OfType<TraitDocument>().First(t => t.Icon is not null);

    private BlessingDocument Blessing =>
        export.Store.Entities.OfType<BlessingDocument>().First(b => b.Icon is not null);

    private static TooltipQuery Asked(string? state) => new() { State = state };

    private IRenderedComponent<Tooltip> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, query));

    [Fact]
    public void A_hero_trait_nothing_asks_about_wears_the_selected_node()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Tooltip.StateFrame(Trait, Asked(null)));
    }

    [Fact]
    public void A_reader_can_ask_for_the_state_the_tree_would_draw()
    {
        Assert.Equal(export.Chrome.TraitNode("available"), export.Tooltip.StateFrame(Trait, Asked("available")));
        Assert.Equal(
            export.Chrome.TraitNode("unavailable"), export.Tooltip.StateFrame(Trait, Asked("unavailable")));
    }

    [Fact]
    public void Only_the_unavailable_node_is_padlocked()
    {
        Assert.True(export.Tooltip.Locked(Trait, Asked("unavailable")));
        Assert.False(export.Tooltip.Locked(Trait, Asked("available")));
        Assert.False(export.Tooltip.Locked(Trait, Asked(null)));
    }

    [Fact]
    public void A_blessing_wears_its_own_plate_and_is_allocated_unless_asked()
    {
        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Tooltip.StateFrame(Blessing, Asked(null)));

        Assert.Equal(
            export.Chrome.BlessingPlate("unallocated"),
            export.Tooltip.StateFrame(Blessing, Asked("unallocated")));

        Assert.False(export.Tooltip.Locked(Blessing, Asked("unallocated")));
    }

    [Fact]
    public void A_word_the_rows_family_does_not_name_leaves_it_at_the_state_it_had()
    {
        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Tooltip.StateFrame(Blessing, Asked("available")));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Tooltip.StateFrame(Trait, Asked("allocated")));
    }

    [Fact]
    public void An_allocated_blessing_shows_the_allocated_art_and_a_hero_traits_art_never_moves()
    {
        Assert.Equal(export.Chrome.BlessingArt(Blessing.Icon, null), export.Tooltip.StateArt(Blessing, Asked(null)));
        Assert.Equal(
            export.Chrome.BlessingArt(Blessing.Icon, null),
            export.Tooltip.StateArt(Blessing, Asked("allocated")));

        Assert.Null(export.Tooltip.StateArt(Blessing, Asked("unallocated")));
        Assert.Null(export.Tooltip.StateArt(Trait, Asked(null)));
        Assert.Null(export.Tooltip.StateArt(Trait, Asked("available")));
    }

    [Fact]
    public void The_drawn_card_shows_the_node_and_its_padlock()
    {
        var padlocked = Draw(Trait, Asked("unavailable"));

        Assert.Contains(
            "T_Buttons_TraitNode_UnavailableDefault.png",
            padlocked.Find(".crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.NotNull(padlocked.Find(".crest .lock"));
        Assert.Empty(Draw(Trait, Asked(null)).FindAll(".crest .lock"));
        Assert.Empty(Draw(Blessing, Asked(null)).FindAll(".crest .lock"));

        Assert.Contains(
            "T_Buttons_BlessingButton_Allocated.png",
            Draw(Blessing, Asked(null)).Find(".crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_node_a_query_string_names_reaches_the_query()
    {
        var query = TooltipQuery.Parse(
            new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["state"] = "available",
            }),
            export.Store.Stats,
            out var error);

        Assert.Null(error);
        Assert.Equal("available", query.State);
    }
}
