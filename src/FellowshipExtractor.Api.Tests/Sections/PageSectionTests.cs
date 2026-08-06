namespace FellowshipExtractor.Api.Tests;

public class IdentityTests : BunitContext
{
    [Fact]
    public void Names_the_type_the_id_and_the_tooltip_address()
    {
        var here = new Bearings
        {
            Under = "/browse?type=items",
            Label = "Items",
            NetId = 5208,
            Address = "/api/tooltip/items/5208",
        };

        var cut = Render<Identity>(p => p.AddCascadingValue(here));

        Assert.Equal("Address", cut.Find("div.cx-panel-head").TextContent);

        var rows = cut.FindAll("table.cx-facts tr");

        Assert.Equal(3, rows.Count);
        Assert.Equal("/browse?type=items", rows[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Items", rows[0].QuerySelector("a")!.TextContent);
        Assert.Equal("5208", rows[1].QuerySelector("td")!.TextContent);
        Assert.Equal("/api/tooltip/items/5208", rows[2].QuerySelector("a")!.GetAttribute("href"));
    }
}

public class NearbyTests : BunitContext
{
    [Fact]
    public void Renders_nothing_when_nowhere_else_to_go()
    {
        var cut = Render<Nearby>(p => p.AddCascadingValue(new Bearings()));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Lists_each_way_out_with_its_count()
    {
        var here = new Bearings
        {
            Related = [("Slot: Head", "/browse?type=items&slot=Head", 1271)],
        };

        var cut = Render<Nearby>(p => p.AddCascadingValue(here));

        Assert.Equal("More like this", cut.Find("div.cx-panel-head").TextContent);

        var pill = cut.Find("ul.cx-pills a");

        Assert.Equal("/browse?type=items&slot=Head", pill.GetAttribute("href"));
        Assert.Equal("1,271", pill.QuerySelector("span.cx-num")!.TextContent);
    }
}

[Collection(Corpus.Name)]
public class VersionsTests : BunitContext
{
    private readonly Export export;

    public VersionsTests(Export export)
    {
        this.export = export;
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Store);
    }

    [Fact]
    public void Renders_nothing_for_an_id_with_one_reading()
    {
        var only = new Entity { Table = "ItemID", NetId = 1 };
        var here = new Bearings { Shown = only, Readings = [only], Noun = "item" };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Groups_the_readings_into_named_versions_and_lists_who_wears_each()
    {
        var cloth = new Entity
        {
            Table = "ItemID", NetId = 1, DisplayName = "Scholar's Headscarf",
            ArmorType = "Cloth", PrimaryStat = "Intellect", Heroes = ["Aeona"],
        };
        var leather = new Entity
        {
            Table = "ItemID", NetId = 1, DisplayName = "Scribe's Headband",
            ArmorType = "Leather", PrimaryStat = "Agility", Heroes = ["Mara"],
        };

        var here = new Bearings
        {
            Shown = cloth,
            Readings = [cloth, leather],
            Reading = "Aeona",
            Page = "/entity/items/1",
            Noun = "item",
        };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Equal("Who can use this item", cut.Find("div.cx-panel-head").FirstChild!.TextContent.Trim());
        Assert.Equal("2 versions", cut.Find("div.cx-panel-head span.cx-num").TextContent);

        var heads = cut.FindAll("p.cx-sub-head");

        Assert.Equal(2, heads.Count);
        Assert.Equal("Cloth / Intellect", heads[0].QuerySelector("span.cx-num")!.TextContent);

        Assert.Equal("color:var(--gold-bright)", heads[0].GetAttribute("style"));
        Assert.False(heads[1].HasAttribute("style"));

        var wearer = cut.Find("div.cx-roster a");

        Assert.Equal("/entity/items/1?hero=Aeona", wearer.GetAttribute("href"));
        Assert.Equal("page", wearer.GetAttribute("aria-current"));
        Assert.Equal("Aeona", wearer.QuerySelector("span.cx-who")!.TextContent);
    }

    [Fact]
    public void Names_one_version_in_the_singular()
    {
        var reading = new Entity
        {
            Table = "ItemID", NetId = 1, DisplayName = "Twin Cowl",
            ArmorType = "Cloth", PrimaryStat = "Intellect", Heroes = ["Aeona"],
        };

        var here = new Bearings
        {
            Shown = reading, Readings = [reading, reading], Page = "/entity/items/1", Noun = "item",
        };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Equal("1 version", cut.Find("div.cx-panel-head span.cx-num").TextContent);
    }

    [Fact]
    public void Gives_a_version_split_by_hero_alone_no_discriminator()
    {
        var first = new Entity { Table = "ItemID", NetId = 1, DisplayName = "Plain Band", Heroes = ["Aeona"] };
        var second = new Entity { Table = "ItemID", NetId = 1, DisplayName = "Plain Band", Heroes = ["Mara"] };

        var here = new Bearings
        {
            Shown = first, Readings = [first, second], Page = "/entity/items/1", Noun = "item",
        };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Single(cut.FindAll("p.cx-sub-head"));
        Assert.Null(cut.Find("p.cx-sub-head").QuerySelector("span.cx-num"));
        Assert.Equal(2, cut.FindAll("div.cx-roster a").Count);
    }
}
