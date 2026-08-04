namespace FellowshipExtractor.Api.Tests;

public class RuleTests : BunitContext
{
    [Fact]
    public void Renders_a_rule()
    {
        var cut = Render<Rule>();

        cut.MarkupMatches("<hr class=\"cx-rule\" />");
    }
}
