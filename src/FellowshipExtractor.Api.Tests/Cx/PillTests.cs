namespace FellowshipExtractor.Api.Tests;

public class PillTests : BunitContext
{
    [Fact]
    public void Renders_a_span_when_there_is_no_destination()
    {
        var cut = Render<Pill>(p => p.AddChildContent("Stat Crit"));

        cut.MarkupMatches("<li><span>Stat Crit</span></li>");
    }

    [Fact]
    public void Renders_an_anchor_when_there_is_a_destination()
    {
        var cut = Render<Pill>(p => p
            .Add(x => x.Href, "/browse?set=Ironhide")
            .AddChildContent("Ironhide"));

        cut.MarkupMatches("<li><a href=\"/browse?set=Ironhide\">Ironhide</a></li>");
    }

    [Fact]
    public void Omits_the_style_attribute_when_there_is_no_colour()
    {
        var cut = Render<Pill>(p => p.AddChildContent("Fire"));

        Assert.False(cut.Find("li > span").HasAttribute("style"));
    }

    [Fact]
    public void Writes_the_colour_it_is_given_including_inherit()
    {
        var cut = Render<Pill>(p => p.Add(x => x.Colour, "inherit").AddChildContent("Tank"));

        cut.MarkupMatches("<li><span style=\"color:inherit\">Tank</span></li>");
    }

    [Fact]
    public void Renders_the_badge_with_no_space_before_it()
    {
        var cut = Render<Pill>(p => p
            .Add(x => x.Href, "/items/5146")
            .Add(x => x.Badge, "Cloth / Intellect")
            .AddChildContent("Webbed Robe"));

        Assert.Equal(
            "<li><a href=\"/items/5146\">Webbed Robe" +
            "<span class=\"cx-num\">Cloth / Intellect</span></a></li>",
            cut.Markup.Bare());
    }
}
