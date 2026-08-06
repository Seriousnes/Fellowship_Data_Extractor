namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class RanksTests : BunitContext
{
    public RanksTests(Export export) => Services.AddSingleton(export.View);

    private static TraitDocument WithOneRun() => new()
    {
        Id = 1,
        RankRuns =
        [
            new RankRun("10/20/30", ["10", "20", "30"])
            {
                Values = [1d, 2d, 3d],
                Transform = "Add",
                Ticks = 5,
                Disagrees = true,
            },
        ],
    };

    [Fact]
    public void Renders_nothing_without_runs()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(new TraitDocument { Id = 1 }));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_panel_its_badge_its_note_and_every_row()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(WithOneRun()));

        cut.MarkupMatches(
            "<div class=\"cx-panel\">" +
            "<div class=\"cx-panel-head\">Rank progression<span class=\"cx-num\">1</span></div>" +
            "<p class=\"cx-note\">The slash-separated numbers the description prints, one entry per run " +
            "of them, matched to the curve behind it. The printed row is what the game shows; the " +
            "values are what it computes with.</p>" +
            "<hr class=\"cx-rule\" />" +
            "<ul class=\"cx-kv\">" +
            "<li><span class=\"cx-k\">Printed</span>" +
            "<span class=\"cx-v cx-prose\">10<span> / </span>20<span> / </span>30</span></li>" +
            "<li><span class=\"cx-k\">Shipped values</span><span class=\"cx-v\">1 / 2 / 3</span></li>" +
            "<li><span class=\"cx-k\">Arithmetic</span><span class=\"cx-v\">Add over 5 ticks</span></li>" +
            "<li><span class=\"cx-k\">Note</span>" +
            "<span class=\"cx-v cx-miss\">printed and computed values disagree</span></li>" +
            "</ul></div>");
    }

    [Fact]
    public void Separates_the_printed_values_with_a_slash_and_no_newline()
    {
        var cut = Render<Ranks>(p => p.AddCascadingValue(WithOneRun()));

        var printed = cut.Find("span.cx-v.cx-prose").OuterHtml.Bare();

        Assert.Equal(
            "<span class=\"cx-v cx-prose\">10<span> / </span>20<span> / </span>30</span>",
            printed);
        Assert.DoesNotContain("\n", printed);
    }

    [Fact]
    public void Renders_only_the_printed_row_when_a_run_carries_nothing_else()
    {
        var entity = new TraitDocument { Id = 1, RankRuns = [new RankRun("10", ["10"])] };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Single(cut.FindAll("ul.cx-kv > li"));
        Assert.Equal("Printed", cut.Find("span.cx-k").TextContent);
    }

    [Fact]
    public void Draws_a_missing_value_as_a_dash()
    {
        var entity = new TraitDocument
        {
            Id = 1,
            RankRuns = [new RankRun("10/20", ["10", "20"]) { Values = [1d, null] }],
        };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Equal("1 / -", cut.FindAll("span.cx-v")[1].TextContent);
    }

    [Fact]
    public void Renders_a_rule_before_every_run()
    {
        var entity = new TraitDocument
        {
            Id = 1,
            RankRuns = [new RankRun("10", ["10"]), new RankRun("20", ["20"])],
        };

        var cut = Render<Ranks>(p => p.AddCascadingValue(entity));

        Assert.Equal(2, cut.FindAll("hr.cx-rule").Count);
        Assert.Equal(2, cut.FindAll("ul.cx-kv").Count);
    }
}
