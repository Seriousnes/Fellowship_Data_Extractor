namespace FellowshipExtractor.Api.Tests;

public class FactsTests : BunitContext
{
    [Fact]
    public void Renders_a_facts_table()
    {
        var cut = Render<Facts>(p => p.AddChildContent("<tr><th>Type</th><td>Items</td></tr>"));

        cut.MarkupMatches("<table class=\"cx-facts\"><tr><th>Type</th><td>Items</td></tr></table>");
    }

    [Fact]
    public void Renders_an_empty_table_when_it_has_no_rows()
    {
        var cut = Render<Facts>();

        Assert.Equal("<table class=\"cx-facts\"></table>", cut.Markup.Bare());
    }
}
