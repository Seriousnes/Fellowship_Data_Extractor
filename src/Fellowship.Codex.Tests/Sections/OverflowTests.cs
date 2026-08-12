namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class OverflowTests : BunitContext
{
    public OverflowTests(Export export) => Services.AddSingleton(export.View);

    private static GemTypeDocument WithOverflow(string? description) =>
        new() { Id = 1, Type = GemType.Amethyst, Attribute = "Strength", OverflowDescription = description };

    [Fact]
    public void Renders_nothing_without_an_overflow()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue(WithOverflow(null)));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_overflow_sentence_in_its_own_panel()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue(WithOverflow("grants nothing further")));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Past max power</div>" +
            "<p class=\"cx-prose\">grants nothing further</p></div>");
    }

    [Fact]
    public void Renders_the_sentence_with_no_whitespace_of_its_own()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue(WithOverflow("one two")));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
    }
}
