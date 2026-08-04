namespace FellowshipExtractor.Api.Tests;

public class ChipTests : BunitContext
{
    [Fact]
    public void Renders_a_chip_with_no_colour()
    {
        var cut = Render<Chip>(p => p.AddChildContent("Cloth"));

        cut.MarkupMatches("<span class=\"cx-chip\">Cloth</span>");
    }

    [Fact]
    public void Renders_a_chip_with_a_colour()
    {
        var cut = Render<Chip>(p => p.Add(x => x.Colour, "#8AC6FF").AddChildContent("Ardeos"));

        cut.MarkupMatches("<span class=\"cx-chip\" style=\"color:#8AC6FF\">Ardeos</span>");
    }

    [Fact]
    public void Omits_the_style_attribute_when_there_is_no_colour()
    {
        var cut = Render<Chip>(p => p.AddChildContent("+2"));

        Assert.False(cut.Find("span.cx-chip").HasAttribute("style"));
    }
}
