namespace FellowshipExtractor.Api.Tests;

public class CountBadgeTests : BunitContext
{
    [Fact]
    public void Renders_a_num_span()
    {
        var cut = Render<CountBadge>(p => p.Add(x => x.Text, "12"));

        cut.MarkupMatches("<span class=\"cx-num\">12</span>");
    }

    [Fact]
    public void Renders_text_that_is_not_a_count()
    {
        var cut = Render<CountBadge>(p => p.Add(x => x.Text, "Cloth / Intellect"));

        cut.MarkupMatches("<span class=\"cx-num\">Cloth / Intellect</span>");
    }
}
