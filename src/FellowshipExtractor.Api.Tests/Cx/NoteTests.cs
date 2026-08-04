namespace FellowshipExtractor.Api.Tests;

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
            "belongs to <a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a>"));

        cut.MarkupMatches(
            "<p class=\"cx-note\">belongs to " +
            "<a href=\"/entity/items/12\" data-tip=\"/api/tooltip/items/12\">Old Boots</a></p>");
    }
}
