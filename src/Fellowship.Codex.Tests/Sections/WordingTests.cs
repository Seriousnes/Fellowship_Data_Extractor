namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class WordingTests : BunitContext
{
    public WordingTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_text()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new AbilityDocument { Id = 1 }));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_description_in_a_text_panel()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new AbilityDocument { Id = 1, Description = "one two" }));

        cut.MarkupMatches(
            "<div class=\"cx-panel\"><div class=\"cx-panel-head\">Text</div>" +
            "<p class=\"cx-prose\">one two</p></div>");
    }

    [Fact]
    public void Renders_the_description_with_exactly_one_space_and_no_newline()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new AbilityDocument { Id = 1, Description = "one two" }));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
    }

    [Fact]
    public void Renders_a_leading_rule_before_each_detail_line()
    {
        var entity = new AbilityDocument { Id = 1, Description = "head", Detail = ["first", "second"] };

        var cut = Render<Wording>(p => p.AddCascadingValue(entity));

        Assert.Equal(3, cut.FindAll("p.cx-prose").Count);
        Assert.Equal(2, cut.FindAll("hr.cx-rule").Count);
    }

    [Fact]
    public void Renders_detail_lines_with_no_description()
    {
        var cut = Render<Wording>(p => p.AddCascadingValue(new AbilityDocument { Id = 1, Detail = ["only"] }));

        Assert.Single(cut.FindAll("p.cx-prose"));
        Assert.Equal("<p class=\"cx-prose\">only</p>", cut.Find("p.cx-prose").OuterHtml.Bare());
    }

    [Fact]
    public void Renders_a_rich_text_run_as_a_direct_child_of_the_prose_element()
    {
        var entity = new AbilityDocument { Id = 1, Description = "grants you <rt.warning>+3%</> haste" };

        var cut = Render<Wording>(p => p.AddCascadingValue(entity));

        var run = cut.Find("p.cx-prose span.rt");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("cx-prose", run.ParentElement.ClassList);
    }
}
