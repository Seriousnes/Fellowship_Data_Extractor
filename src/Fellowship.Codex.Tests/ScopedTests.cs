namespace Fellowship.Codex.Tests;

public class ScopedTests : BunitContext
{
    [Fact]
    public void Strips_the_scope_attribute_a_component_renders()
    {
        var cut = Render<Note>(p => p.AddChildContent("plain"));

        Assert.Matches(@"\sb-[a-z0-9]{10}", cut.Markup);
        Assert.Equal("<p class=\"cx-note\">plain</p>", cut.Markup.Bare());
    }

    [Fact]
    public void Strips_the_scope_attribute_as_the_parsed_dom_serialises_it()
    {
        var cut = Render<Note>(p => p.AddChildContent("plain"));

        var html = cut.Find("p.cx-note").OuterHtml;

        Assert.Matches(@"\sb-[a-z0-9]{10}=""""", html);
        Assert.Equal("<p class=\"cx-note\">plain</p>", html.Bare());
    }

    [Fact]
    public void Leaves_a_data_attribute_a_class_and_the_text_between_two_elements_alone()
    {
        var slug = EntityTypes.Slug(EntityType.Item);
        var markup =
            "<p class=\"cx-note\">belongs to " +
            $"<a href=\"/{slug}/12\" data-tip=\"/api/{slug}/12/tooltip\">Old Boots</a>" +
            ", which is where it was taken from.</p>";

        Assert.Equal(markup, markup.Bare());
    }
}
