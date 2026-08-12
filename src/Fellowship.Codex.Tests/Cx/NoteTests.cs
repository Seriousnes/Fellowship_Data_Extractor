namespace Fellowship.Codex.Tests;

public class NoteTests : BunitContext
{
    [Fact]
    public void Renders_a_note_paragraph()
    {
        var cut = Render<Note>(p => p.AddChildContent("A slot's weight is its share of the budget."));

        cut.MarkupMatches("<p class=\"cx-note\">A slot's weight is its share of the budget.</p>");
    }

    [Fact]
    public void Renders_inline_markup_inside_the_note()
    {
        var cut = Render<Note>(p => p.AddChildContent(
            "belongs to <a href=\"/item/12\" data-tip=\"/api/item/12/tooltip\">Old Boots</a>"));

        cut.MarkupMatches(
            "<p class=\"cx-note\">belongs to " +
            "<a href=\"/item/12\" data-tip=\"/api/item/12/tooltip\">Old Boots</a></p>");
    }
}
