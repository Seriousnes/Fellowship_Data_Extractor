namespace FellowshipExtractor.Api.Tests;

public class EmptyTests : BunitContext
{
    [Fact]
    public void Renders_a_headline_and_a_detail_line()
    {
        var cut = Render<Empty>(p => p
            .Add(x => x.Said, "Nothing in the build answers that.")
            .AddChildContent("Remove a filter and try again."));

        cut.MarkupMatches(
            "<p class=\"cx-empty\"><b>Nothing in the build answers that.</b><br />" +
            "Remove a filter and try again.</p>");
    }

    [Fact]
    public void Renders_neither_the_headline_nor_its_break_when_none_is_said()
    {
        var cut = Render<Empty>(p => p.AddChildContent("Loading..."));

        Assert.Equal("<p class=\"cx-empty\">Loading...</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_a_link_as_the_detail_line()
    {
        var cut = Render<Empty>(p => p
            .Add(x => x.Said, "No such id.")
            .AddChildContent("<a href=\"/browse\">Browse everything</a>"));

        cut.MarkupMatches(
            "<p class=\"cx-empty\"><b>No such id.</b><br />" +
            "<a href=\"/browse\">Browse everything</a></p>");
    }
}
