namespace Fellowship.Codex.Tests;

public class ProseTests : BunitContext
{
    [Fact]
    public void Renders_two_words_with_exactly_one_space_and_no_newline()
    {
        var cut = Render<Prose>(p => p.AddChildContent("one two"));

        Assert.Equal("<p class=\"cx-prose\">one two</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_a_rich_text_run_as_a_direct_child_of_the_prose_element()
    {
        var cut = Render<Prose>(p => p.AddChildContent(
            "grants you <span class=\"rt rt-warning\">+3%</span> haste"));

        var run = cut.Find("span.rt-warning");

        Assert.Equal("P", run.ParentElement!.TagName);
        Assert.Contains("cx-prose", run.ParentElement.ClassList);
    }

    [Fact]
    public void Keeps_a_newline_the_game_text_carries()
    {
        var cut = Render<Prose>(p => p.AddChildContent("first\nsecond"));

        Assert.Equal("<p class=\"cx-prose\">first\nsecond</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_no_scope_attribute_because_it_owns_no_stylesheet()
    {
        var cut = Render<Prose>(p => p.AddChildContent("one two"));

        Assert.DoesNotMatch(@"\sb-[a-z0-9]{10}", cut.Markup);
    }
}
