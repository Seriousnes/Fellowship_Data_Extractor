namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class OverflowTests : BunitContext
{
    public OverflowTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_an_overflow()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_overflow_sentence_in_its_own_panel()
    {
        var entity = new Entity { GemOverflow = new GemOverflow("grants nothing further") };

        var cut = Render<Overflow>(p => p.AddCascadingValue(entity));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Past max power</div>" +
            "<p class=\"cx-prose\">grants nothing further</p></div>");
    }

    [Fact]
    public void Renders_the_sentence_with_no_whitespace_of_its_own()
    {
        var entity = new Entity { GemOverflow = new GemOverflow("one two") };

        var cut = Render<Overflow>(p => p.AddCascadingValue(entity));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
    }
}
