namespace FellowshipExtractor.Api.Tests;

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
}
