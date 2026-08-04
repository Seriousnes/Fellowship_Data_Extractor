namespace FellowshipExtractor.Api.Tests;

public class IconTests : BunitContext
{
    [Fact]
    public void Renders_a_lazy_image_with_an_empty_alt()
    {
        var cut = Render<Icon>(p => p.Add(x => x.Src, "http://localhost/icons/boots.png"));

        cut.MarkupMatches("<img alt=\"\" loading=\"lazy\" src=\"http://localhost/icons/boots.png\" />");
    }
}
