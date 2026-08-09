using FellowshipExtractor.Api.Components.Tooltip;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class TraitCardTests : BunitContext
{
    private readonly Export export;

    public TraitCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
    }

    private IEnumerable<TraitDocument> Traits => export.Store.Entities.OfType<TraitDocument>();

    private TraitDocument Written => Traits.First(row => row.Icon is not null && row.Description is not null);

    private IRenderedComponent<Tooltip> Draw(EntityDocument document) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, document).Add(x => x.Query, new TooltipQuery()));

    [Fact]
    public void A_trait_is_drawn_by_a_card_of_its_own()
    {
        var drawn = Draw(Written);
        var root = drawn.Find($".{TooltipView.Root}");

        Assert.Contains("fx-trait", root.ClassList, StringComparer.Ordinal);
        Assert.Contains("plain", root.ClassList, StringComparer.Ordinal);
        Assert.Empty(drawn.FindAll("figure.fx-slot"));
        Assert.Equal(PublicTypes.NameOf(Written), drawn.Find(".fx-trait .head .title").TextContent.Trim());
    }

    [Fact]
    public void The_card_is_chosen_by_the_type_and_not_by_the_art()
    {
        var drawn = Draw(Traits.First(row => row.Icon is null));

        Assert.NotNull(drawn.Find(".fx-trait .card"));
        Assert.Empty(drawn.FindAll(".fx-trait-crest"));
        Assert.Empty(drawn.FindAll("figure.fx-slot"));
    }

    [Fact]
    public void The_crest_sits_in_the_card_at_the_width_the_node_is_drawn()
    {
        Assert.Contains(
            "--fx-node-width:72px",
            Draw(Written).Find(".fx-trait .card > .fx-trait-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_prose_reads_the_description_in_the_styles_the_build_ships()
    {
        var styled = Traits.First(row =>
            row.Description is { } text && text.Contains("<rt.", StringComparison.Ordinal));

        var prose = Draw(styled).Find(".fx-trait .lines .desc");

        Assert.NotEmpty(prose.QuerySelectorAll("span.rt"));
        Assert.DoesNotContain("<rt.", prose.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_trait_the_build_writes_nothing_about_draws_no_prose()
    {
        Assert.Empty(Draw(Traits.First(row => row.Description is null)).FindAll(".fx-trait .lines"));
    }

    [Fact]
    public void The_card_asks_for_none_of_the_rarity_chrome()
    {
        var drawn = Draw(Written);
        var style = drawn.Find($".{TooltipView.Root}").GetAttribute("style") ?? string.Empty;

        Assert.Contains("--fx-frame:none", style, StringComparison.Ordinal);
        Assert.Contains("--fx-header:none", style, StringComparison.Ordinal);
        Assert.Empty(drawn.FindAll(".fx-trait .orn"));
    }
}
