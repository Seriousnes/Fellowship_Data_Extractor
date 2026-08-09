namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class IconTests : BunitContext
{
    private readonly Export Export;

    public IconTests(Export export)
    {
        Export = export;
        Services.AddSingleton(export.View);
    }

    private static string Style(IRenderedComponent<Icon> drawn) =>
        drawn.Find("figure.fx-slot").GetAttribute("style") ?? string.Empty;

    [Fact]
    public void An_icon_no_rung_names_wears_the_ability_chrome_and_carries_its_art_lazily()
    {
        var drawn = Render<Icon>(p => p
            .Add(x => x.Src, "http://localhost/icons/boots.png")
            .Add(x => x.Width, 34));

        Assert.Contains("plain", drawn.Find("figure.fx-slot").ClassList);

        var style = Style(drawn);

        Assert.Contains($"--fx-slot-backing:url({Export.Chrome.SlotBacking(null)})", style, StringComparison.Ordinal);
        Assert.Contains($"--fx-slot-frame:url({Export.Chrome.SlotFrame(null)})", style, StringComparison.Ordinal);
        Assert.DoesNotContain("--fx-slot-bevel", style, StringComparison.Ordinal);
        Assert.Contains("--fx-slot-width:34px", style, StringComparison.Ordinal);

        var art = drawn.Find("img");

        Assert.Equal(string.Empty, art.GetAttribute("alt"));
        Assert.Equal("lazy", art.GetAttribute("loading"));
        Assert.Equal("http://localhost/icons/boots.png", art.GetAttribute("src"));
    }

    [Fact]
    public void A_tooltip_asks_for_its_art_eagerly_because_the_card_is_drawn_where_it_is_read()
    {
        var drawn = Render<Icon>(p => p
            .Add(x => x.Src, "http://localhost/icons/boots.png")
            .Add(x => x.Lazy, false));

        Assert.Equal("eager", drawn.Find("img").GetAttribute("loading"));
    }

    [Fact]
    public void An_icon_a_rung_names_wears_that_rung_and_the_bevel_over_it()
    {
        var rung = Export.Store.Settings.Rarities[0].Name;

        var drawn = Render<Icon>(p => p
            .Add(x => x.Src, "http://localhost/icons/boots.png")
            .Add(x => x.Rarity, rung));

        Assert.DoesNotContain("plain", drawn.Find("figure.fx-slot").ClassList);

        var style = Style(drawn);

        Assert.Contains($"--fx-slot-backing:url({Export.Chrome.SlotBacking(rung)})", style, StringComparison.Ordinal);
        Assert.Contains($"--fx-slot-frame:url({Export.Chrome.SlotFrame(rung)})", style, StringComparison.Ordinal);
        Assert.Contains($"--fx-slot-bevel:url({Export.Chrome.SlotBevel(rung)})", style, StringComparison.Ordinal);
    }

    [Fact]
    public void An_entity_carrying_no_art_draws_the_slot_and_nothing_in_it()
    {
        var drawn = Render<Icon>(p => p.Add(x => x.Src, null));

        Assert.NotNull(drawn.Find("figure.fx-slot"));
        Assert.Empty(drawn.FindAll("img"));
    }
}
