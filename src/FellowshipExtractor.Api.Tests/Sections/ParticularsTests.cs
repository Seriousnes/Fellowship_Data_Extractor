namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ParticularsTests : BunitContext
{
    public ParticularsTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void Renders_nothing_when_the_entity_carries_none_of_it()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Renders_the_facts_in_two_tables_with_the_odd_row_in_the_first()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Cooldown", "2.5", "s", null, null),
            ("Range", "30", "m", null, null),
            ("Cost", "40", null, null, null),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var tables = cut.FindAll("table.cx-facts");

        Assert.Equal(2, tables.Count);
        Assert.Equal(2, tables[0].QuerySelectorAll("tr").Length);
        Assert.Single(tables[1].QuerySelectorAll("tr"));
    }

    [Fact]
    public void Renders_an_empty_second_table_for_a_single_fact()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Cooldown", "2.5", "s", null, null),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var tables = cut.FindAll("table.cx-facts");

        Assert.Equal(2, tables.Count);
        Assert.Empty(tables[1].Children);
    }

    [Fact]
    public void Renders_a_linked_fact_cell_with_its_tip()
    {
        IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> facts =
        [
            ("Resource", "Mana", null, "/browse?resource=Mana", "/api/tooltip/effect/9"),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Facts, facts));

        var link = cut.Find("table.cx-facts a");

        Assert.Equal("/browse?resource=Mana", link.GetAttribute("href"));
        Assert.Equal("/api/tooltip/effect/9", link.GetAttribute("data-tip"));
        Assert.Equal("Mana", link.TextContent);
    }

    [Fact]
    public void Opens_with_a_rule_when_there_are_no_facts()
    {
        IReadOnlyList<(string Label, string Href)> schools = [("Fire", "/browse?type=ability&school=Fire")];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        var head = cut.Find("div.cx-panel-head");

        Assert.Equal("HR", head.NextElementSibling!.TagName);
        Assert.Contains("cx-rule", head.NextElementSibling.ClassList);
    }

    [Fact]
    public void Names_one_school_in_the_singular()
    {
        IReadOnlyList<(string Label, string Href)> schools = [("Fire", "/browse?type=ability&school=Fire")];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        Assert.Equal("School", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?type=ability&school=Fire", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_two_schools_in_the_plural()
    {
        IReadOnlyList<(string Label, string Href)> schools =
        [
            ("Fire", "/browse?type=ability&school=Fire"),
            ("Frost", "/browse?type=ability&school=Frost"),
        ];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Schools, schools));

        Assert.Equal("Schools", cut.Find("p.cx-sub-head").TextContent);
    }

    [Fact]
    public void Links_each_hero_into_the_browser_with_an_escaped_name()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { Heroes = ["Lady Sunbloom"] }));

        Assert.Equal("Heroes", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?hero=Lady%20Sunbloom", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_the_set_and_its_piece_count()
    {
        var entity = new Entity { SetBonus = ["Ironhide"], SetCount = 4 };

        var cut = Render<Particulars>(p => p.AddCascadingValue(entity));

        Assert.Equal("Set (4 pieces)", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("/browse?set=Ironhide", cut.Find("ul.cx-pills a").GetAttribute("href"));
    }

    [Fact]
    public void Names_the_set_without_a_piece_count()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { SetBonus = ["Ironhide"] }));

        Assert.Equal("Set", cut.Find("p.cx-sub-head").TextContent);
    }

    [Fact]
    public void Draws_the_stats_it_scales_off_and_gives_them_no_destination()
    {
        IReadOnlyList<string> scaling = ["Stat Crit"];

        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity()).Add(x => x.Scaling, scaling));

        Assert.Equal("Scales off", cut.Find("p.cx-sub-head").TextContent);
        Assert.Equal("Stat Crit", cut.Find("ul.cx-pills li > span").TextContent);
    }

    [Fact]
    public void Offers_roles_then_attributes_in_one_list()
    {
        var entity = new Entity
        {
            AllowedRoles = ["Tank", "Healer"],
            AllowedAttributes = ["Strength", "Intellect"],
        };

        var cut = Render<Particulars>(p => p.AddCascadingValue(entity));

        Assert.Single(cut.FindAll("ul.cx-pills"));

        var items = cut.FindAll("ul.cx-pills > li");

        Assert.Equal(4, items.Count);
        Assert.Equal("Tank", items[0].TextContent);
        Assert.Equal("Healer", items[1].TextContent);
        Assert.Equal("Strength", items[2].TextContent);
        Assert.Equal("Intellect", items[3].TextContent);
        Assert.True(items[0].FirstElementChild!.HasAttribute("style"));
        Assert.True(items[1].FirstElementChild!.HasAttribute("style"));
        Assert.False(items[2].FirstElementChild!.HasAttribute("style"));
        Assert.False(items[3].FirstElementChild!.HasAttribute("style"));
    }

    [Fact]
    public void Opens_the_panel_and_draws_only_its_head_for_attributes_alone()
    {
        var cut = Render<Particulars>(p => p.AddCascadingValue(new Entity { AllowedAttributes = ["Strength"] }));

        cut.MarkupMatches("<div class=\"cx-panel\"><div class=\"cx-panel-head\">Details</div></div>");
    }
}
