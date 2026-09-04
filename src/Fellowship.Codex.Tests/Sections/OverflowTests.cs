namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class OverflowTests : BunitContext
{
    private readonly Export export;

    public OverflowTests(Export exported)
    {
        export = exported;
        Services.AddSingleton(exported.View);
    }

    private GemTypeDocument Cut =>
        export.Store.Entities.OfType<GemTypeDocument>().First(stone => stone.OverflowDescription is not null);

    private GemDocument Stone =>
        export.Store.Entities.OfType<GemDocument>().First(gem => gem.Type == Cut.Type);

    [Fact]
    public void Renders_nothing_for_a_document_that_names_no_gem_type()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue<EntityDocument>(new ItemDocument { Id = 1 }));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_overflow_sentence_of_a_gems_own_type_in_its_own_panel()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue<EntityDocument>(Stone));

        Assert.Equal("Past max power", cut.Find("div.cx-panel-head").TextContent);
        Assert.Equal(
            export.View.PlainText(Cut.OverflowDescription, Cut.OverflowPlaceholders, []),
            cut.Find("p.cx-prose").TextContent);
    }

    [Fact]
    public void Renders_the_sentence_with_no_whitespace_of_its_own()
    {
        var cut = Render<Overflow>(p => p.AddCascadingValue<EntityDocument>(Stone));

        var prose = cut.Find("p.cx-prose").OuterHtml.Bare();

        Assert.StartsWith("<p class=\"cx-prose\">", prose, StringComparison.Ordinal);
        Assert.DoesNotContain("<p class=\"cx-prose\"> ", prose, StringComparison.Ordinal);
        Assert.DoesNotContain(" </p>", prose, StringComparison.Ordinal);
    }
}
