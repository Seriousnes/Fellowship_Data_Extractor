namespace Fellowship.Codex.Tests;

public class KnobTests : BunitContext
{
    [Fact]
    public void Renders_a_label_around_one_control()
    {
        var cut = Render<Knob>(p => p
            .Add(x => x.Caption, "Item level")
            .AddChildContent("<input type=\"number\" data-knob=\"itemLevel\" />"));

        cut.MarkupMatches(
            "<label class=\"cx-knob\"><span>Item level</span>" +
            "<input type=\"number\" data-knob=\"itemLevel\" /></label>");
    }

    [Fact]
    public void Renders_a_div_around_a_button_group()
    {
        var cut = Render<Knob>(p => p
            .Add(x => x.Caption, "Rarity")
            .Add(x => x.As, "div")
            .AddChildContent("<div class=\"cx-rungs\"></div>"));

        cut.MarkupMatches(
            "<div class=\"cx-knob\"><span>Rarity</span><div class=\"cx-rungs\"></div></div>");
    }

    [Fact]
    public void Renders_the_caption_as_the_first_child()
    {
        var cut = Render<Knob>(p => p
            .Add(x => x.Caption, "Hero")
            .AddChildContent("<select></select>"));

        Assert.Equal("SPAN", cut.Find(".cx-knob").FirstElementChild!.TagName);
    }
}
