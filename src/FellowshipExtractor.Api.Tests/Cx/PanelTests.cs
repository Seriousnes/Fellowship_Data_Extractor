namespace FellowshipExtractor.Api.Tests;

public class PanelTests : BunitContext
{
    [Fact]
    public void Renders_a_headless_panel()
    {
        var cut = Render<Panel>(p => p.AddChildContent("<p>body</p>"));

        cut.MarkupMatches("<div class=\"cx-panel\"><p>body</p></div>");
    }

    [Fact]
    public void Renders_a_head_with_no_badge()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Address").AddChildContent("<p>body</p>"));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Address</div><p>body</p></div>");
    }

    [Fact]
    public void Renders_a_head_with_a_badge_and_no_space_before_it()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Stat slots").Add(x => x.Badge, "3"));

        Assert.Equal(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Stat slots" +
            "<span class=\"cx-num\">3</span></div></div>",
            cut.Markup.Bare());
    }

    [Fact]
    public void Renders_the_badge_as_the_last_child_of_the_head()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Legendary stats").Add(x => x.Badge, "2"));

        var head = cut.Find("div.cx-panel-head");

        Assert.Contains("cx-num", head.LastElementChild!.ClassList);
    }

    [Fact]
    public void Renders_an_aside_with_an_extra_class_and_an_aria_label()
    {
        var cut = Render<Panel>(p => p
            .Add(x => x.As, "aside")
            .Add(x => x.Class, "cx-rail")
            .Add(x => x.Aria, "Filters")
            .Add(x => x.Head, "Refine"));

        cut.MarkupMatches(
            "<aside class=\"cx-panel cx-rail\" aria-label=\"Filters\">" +
            "<div class=\"cx-panel-head\">Refine</div></aside>");
    }

    [Fact]
    public void Renders_a_details_root_with_a_summary_head()
    {
        var cut = Render<Panel>(p => p
            .Add(x => x.As, "details")
            .Add(x => x.Head, "The exported record")
            .AddChildContent("<pre>{}</pre>"));

        cut.MarkupMatches(
            "<details class=\"cx-panel\">" +
            "<summary class=\"cx-panel-head\">The exported record</summary><pre>{}</pre></details>");
    }

    [Fact]
    public void Omits_the_aria_label_when_none_is_given()
    {
        var cut = Render<Panel>(p => p.Add(x => x.Head, "Details"));

        Assert.False(cut.Find("div.cx-panel").HasAttribute("aria-label"));
    }
}
