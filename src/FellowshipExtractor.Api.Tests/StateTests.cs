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
        Services.AddSingleton(export.View);
    }

    private TraitDocument Trait => export.Store.Entities.OfType<TraitDocument>().First(t => t.Icon is not null);

    private static TooltipQuery Asked(string? state) => new() { State = state };

    private IRenderedComponent<Tooltip> Draw(EntityDocument document, TooltipQuery query) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, query));

    [Fact]
    public void A_hero_trait_nothing_asks_about_wears_the_selected_node()
    {
        Assert.Contains(
            "T_Buttons_TraitNode_SelectedDefault.png",
            Draw(Trait, Asked(null)).Find(".fx-trait-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_reader_can_ask_for_the_state_the_tree_would_draw()
    {
        Assert.Contains(
            "T_Buttons_TraitNode_AvailableDefault.png",
            Draw(Trait, Asked("available")).Find(".fx-trait-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Contains(
            "T_Buttons_TraitNode_UnavailableDefault.png",
            Draw(Trait, Asked("unavailable")).Find(".fx-trait-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_unavailable_node_is_padlocked()
    {
        Assert.True(ChromeAssets.Padlocked("unavailable"));
        Assert.True(ChromeAssets.Padlocked("UNAVAILABLE"));
        Assert.False(ChromeAssets.Padlocked("available"));
        Assert.False(ChromeAssets.Padlocked("selected"));
        Assert.False(ChromeAssets.Padlocked(null));
        Assert.False(ChromeAssets.Padlocked("allocated"));
    }

    [Fact]
    public void A_word_the_hero_traits_family_does_not_name_leaves_it_at_the_state_it_had()
    {
        Assert.Contains(
            "T_Buttons_TraitNode_SelectedDefault.png",
            Draw(Trait, Asked("allocated")).Find(".fx-trait-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_drawn_card_shows_the_node_and_its_padlock()
    {
        var padlocked = Draw(Trait, Asked("unavailable"));

        Assert.EndsWith(
            "T_Icons_Locked.png",
            padlocked.Find(".fx-trait-crest .lock").GetAttribute("src") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Empty(Draw(Trait, Asked(null)).FindAll(".fx-trait-crest .lock"));
    }

    [Fact]
    public void Only_the_padlocked_node_draws_its_art_desaturated()
    {
        Assert.Contains(
            "locked",
            Draw(Trait, Asked("unavailable")).Find(".fx-trait-crest").ClassList,
            StringComparer.Ordinal);

        Assert.DoesNotContain(
            "locked",
            Draw(Trait, Asked(null)).Find(".fx-trait-crest").ClassList,
            StringComparer.Ordinal);

        Assert.Contains(
            ".fx-trait-crest.locked .well img {\n  filter: grayscale(1);\n}",
            export.Tooltip.Style(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_padlock_sits_outside_the_shape_the_art_is_cut_to()
    {
        var padlocked = Draw(Trait, Asked("unavailable"));

        Assert.Empty(padlocked.FindAll(".fx-trait-crest .well .lock"));
        Assert.NotNull(padlocked.Find(".fx-trait-crest > .lock"));
    }

    [Fact]
    public void The_crest_cuts_its_art_to_the_well_the_plate_leaves_open()
    {
        var drawn = Draw(Trait, Asked(null));

        Assert.Equal(
            export.Tooltip.Art(Trait, null),
            drawn.Find(".fx-trait-crest .well img").GetAttribute("src"));

        Assert.Contains(
            "clip-path: polygon(50% 12.5%, 87.0192307692% 31.25%, 87.0192307692% 68.75%, 50% 87.5%, " +
            "12.9807692308% 68.75%, 12.9807692308% 31.25%)",
            export.Tooltip.Style(),
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
