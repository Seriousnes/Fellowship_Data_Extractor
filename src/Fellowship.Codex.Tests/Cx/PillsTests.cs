namespace Fellowship.Codex.Tests;

public class PillsTests : BunitContext
{
    [Fact]
    public void Wraps_children_in_a_pills_list()
    {
        var cut = Render<Pills>(p => p.AddChildContent("<li><span>Fire</span></li>"));

        cut.MarkupMatches("<ul class=\"cx-pills\"><li><span>Fire</span></li></ul>");
    }

    [Fact]
    public void Holds_items_from_two_sources_in_one_list()
    {
        var cut = Render<Pills>(p => p.AddChildContent(
            "<li><span>Tank</span></li><li><span>Strength</span></li>"));

        Assert.Equal(2, cut.FindAll("ul.cx-pills > li").Count);
    }
}
