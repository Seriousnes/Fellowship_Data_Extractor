namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class StatSlotsTests : BunitContext
{
    public StatSlotsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_without_stats()
    {
        var cut = Render<StatSlots>(p => p.AddCascadingValue(new ItemDocument { Id = 1 }));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_panel_its_badge_its_note_and_its_lines()
    {
        var entity = new ItemDocument
        {
            Id = 1,
            Stats =
            [
                new StatSlot("Stamina", 40) { Kind = "Primary" },
                new StatSlot("StatCrit", 20) { Kind = "Secondary" },
            ],
        };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        cut.MarkupMatches(
            "<div class=\"cx-panel\">" +
            "<div class=\"cx-panel-head\">Stat slots<span class=\"cx-num\">2</span></div>" +
            "<p class=\"cx-note\">A slot's weight is its share of the item's stat budget. The magnitude " +
            "is not in the build: a dynamic item rolls it from its level and rarity when it drops, " +
            "which is what the tooltip above computes.</p>" +
            "<ul class=\"cx-stat-lines\">" +
            "<li class=\"cx-primary\"><span>Stamina</span>" +
            "<span class=\"cx-w\">Primary &middot; weight 40</span></li>" +
            "<li class=\"cx-secondary\"><span>Stat Crit</span>" +
            "<span class=\"cx-w\">Secondary &middot; weight 20</span></li>" +
            "</ul></div>");
    }

    [Fact]
    public void Marks_a_primary_attribute_as_primary()
    {
        var entity = new ItemDocument { Id = 1, Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }] };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        Assert.Contains("cx-primary", cut.Find("ul.cx-stat-lines > li").ClassList);
    }

    [Fact]
    public void Draws_the_slots_in_export_order()
    {
        var entity = new ItemDocument
        {
            Id = 1,
            Stats =
            [
                new StatSlot("StatCrit", 20) { Kind = "Secondary" },
                new StatSlot("Stamina", 40) { Kind = "Primary" },
            ],
        };

        var cut = Render<StatSlots>(p => p.AddCascadingValue(entity));

        var lines = cut.FindAll("ul.cx-stat-lines > li");

        Assert.Equal("Stat Crit", lines[0].FirstElementChild!.TextContent);
        Assert.Equal("Stamina", lines[1].FirstElementChild!.TextContent);
    }
}
