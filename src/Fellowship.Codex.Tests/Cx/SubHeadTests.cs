namespace Fellowship.Codex.Tests;

public class SubHeadTests : BunitContext
{
    [Fact]
    public void Renders_a_sub_head_paragraph()
    {
        var cut = Render<SubHead>(p => p.AddChildContent("Heroes"));

        cut.MarkupMatches("<p class=\"cx-sub-head\">Heroes</p>");
    }

    [Fact]
    public void Renders_composed_child_content()
    {
        var cut = Render<SubHead>(p => p.AddChildContent("Set (4 pieces)"));

        cut.MarkupMatches("<p class=\"cx-sub-head\">Set (4 pieces)</p>");
    }

    [Fact]
    public void Omits_the_style_attribute_when_there_is_no_colour()
    {
        var cut = Render<SubHead>(p => p.AddChildContent("Heroes"));

        Assert.False(cut.Find("p.cx-sub-head").HasAttribute("style"));
    }

    [Fact]
    public void Writes_the_colour_it_is_given()
    {
        var cut = Render<SubHead>(p => p
            .Add(x => x.Colour, "var(--gold-bright)")
            .AddChildContent("Scholar's Headscarf"));

        cut.MarkupMatches(
            "<p class=\"cx-sub-head\" style=\"color:var(--gold-bright)\">Scholar's Headscarf</p>");
    }
}
