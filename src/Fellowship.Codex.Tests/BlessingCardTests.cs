using Fellowship.SDK.Client;

using Fellowship.Codex.Components.Tooltip;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class BlessingCardTests : BunitContext
{
    private const int TheMystic = 706;

    private readonly Export export;

    public BlessingCardTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.Tooltip);
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    private BlessingDocument Mystic =>
        export.Store.Entities.OfType<BlessingDocument>().Single(row => row.Id == TheMystic);

    private IRenderedComponent<Tooltip> Draw(TooltipQuery query) =>
        Render<Tooltip>(p => p.Add(x => x.Entity, (EntityDocument)Mystic).Add(x => x.Query, query));

    [Fact]
    public void The_card_heads_itself_with_the_word_the_build_gives_a_blessing()
    {
        var drawn = Draw(new TooltipQuery());

        Assert.Equal("Blessing", export.Store.Settings.Blessings.Name);
        Assert.Equal(
            export.Store.Settings.Blessings.Name, drawn.Find(".fx-blessing .head .eyebrow").TextContent.Trim());

        Assert.Equal("The Mystic", drawn.Find(".fx-blessing .head .title").TextContent.Trim());
    }

    [Fact]
    public void The_band_says_what_the_row_is_and_which_tier_is_equipped()
    {
        var wording = export.Store.Settings.Blessings;

        Assert.Equal("Hero Blessing", wording.HeroBlessing);
        Assert.Equal("Equipped Tier", wording.TierLevel);
        Assert.Equal(4, wording.Tiers);

        var band = Draw(new TooltipQuery()).Find(".fx-blessing .band");

        Assert.Equal(wording.HeroBlessing, band.QuerySelector(".kind")!.TextContent.Trim());
        Assert.Equal("0 / 4", band.QuerySelector(".count")!.TextContent.Trim());
        Assert.StartsWith(wording.TierLevel, band.QuerySelector(".tier")!.TextContent.Trim(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_tier_a_reader_asks_for_is_the_one_the_band_prints()
    {
        Assert.Equal("2 / 4", Draw(new TooltipQuery { EquippedTier = 2 }).Find(".band .count").TextContent.Trim());
        Assert.Equal("4 / 4", Draw(new TooltipQuery { EquippedTier = 4 }).Find(".band .count").TextContent.Trim());
    }

    [Fact]
    public void The_equipped_tier_the_caller_names_reaches_the_query()
    {
        var query = TooltipRequests.Read(
            new TooltipRequest { EquippedTier = 3 }, export.Store, out var error);

        Assert.Null(error);
        Assert.Equal(3, query.EquippedTier);

        Assert.Null(TooltipRequests.Read(new TooltipRequest(), export.Store, out _).EquippedTier);
    }

    [Fact]
    public void The_prose_reads_the_values_the_build_states_and_is_centred()
    {
        var prose = Draw(new TooltipQuery()).Find(".fx-blessing .prose");

        Assert.Contains("for every 3% Spirit", prose.TextContent, StringComparison.Ordinal);
        Assert.Contains("up to 50% Spirit", prose.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("not in the build", prose.TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_crest_layers_the_state_plate_under_the_art_that_state_names()
    {
        var allocated = Draw(new TooltipQuery()).Find(".fx-blessing-crest");
        var unallocated = Draw(new TooltipQuery { State = "unallocated" }).Find(".fx-blessing-crest");

        Assert.Contains(
            export.Chrome.BlessingPlate("allocated"),
            allocated.GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Contains(
            export.Chrome.BlessingPlate("unallocated"),
            unallocated.GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Equal(
            export.Chrome.BlessingArt(Mystic.Icon, null), allocated.QuerySelector("img")!.GetAttribute("src"));

        Assert.Equal(
            export.View.Icon(Mystic.Icon), unallocated.QuerySelector("img")!.GetAttribute("src"));
    }

    [Fact]
    public void The_crest_draws_at_the_width_it_is_given()
    {
        var narrow = Render<BlessingCrest>(p => p.Add(x => x.Entity, Mystic).Add(x => x.Width, 26));

        Assert.Contains(
            "--fx-plate-width:26px",
            narrow.Find(".fx-blessing-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);

        Assert.Contains(
            "--fx-plate-width:60px",
            Draw(new TooltipQuery()).Find(".fx-blessing-crest").GetAttribute("style") ?? string.Empty,
            StringComparison.Ordinal);
    }
}
