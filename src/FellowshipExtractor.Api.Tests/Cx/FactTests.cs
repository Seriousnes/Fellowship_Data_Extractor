namespace FellowshipExtractor.Api.Tests;

public class FactTests : BunitContext
{
    [Fact]
    public void Renders_a_label_cell_and_a_value_cell()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Type").AddChildContent("Items"));

        Assert.Equal("<tr><th>Type</th><td>Items</td></tr>", cut.Markup.Bare());
    }

    [Fact]
    public void Renders_the_unit_with_no_space_before_it()
    {
        var cut = Render<Fact>(p => p
            .Add(x => x.Label, "Cooldown")
            .Add(x => x.Unit, "s")
            .AddChildContent("2.5"));

        Assert.Equal(
            "<tr><th>Cooldown</th><td>2.5<span class=\"cx-unit\">s</span></td></tr>",
            cut.Markup.Bare());
    }

    [Fact]
    public void Renders_no_unit_span_when_there_is_no_unit()
    {
        var cut = Render<Fact>(p => p.Add(x => x.Label, "Network id").AddChildContent("5146"));

        Assert.DoesNotContain("cx-unit", cut.Markup);
    }

    [Fact]
    public void Renders_a_linked_value()
    {
        var cut = Render<Fact>(p => p
            .Add(x => x.Label, "Resource")
            .AddChildContent(
                "<a href=\"/browse?resource=Mana\" data-tip=\"/api/effect/9/tooltip\">Mana</a>"));

        Assert.Equal(
            "<tr><th>Resource</th><td>" +
            "<a href=\"/browse?resource=Mana\" data-tip=\"/api/effect/9/tooltip\">Mana</a></td></tr>",
            cut.Markup.Bare());
    }
}
