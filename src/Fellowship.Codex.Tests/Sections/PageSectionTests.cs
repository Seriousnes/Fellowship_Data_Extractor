namespace Fellowship.Codex.Tests;

public class IdentityTests : BunitContext
{
    [Fact]
    public void Names_the_type_the_id_and_the_tooltip_address()
    {
        var slug = EntityTypes.Slug(EntityType.Item);
        var here = new Bearings
        {
            Under = $"/browse?type={slug}",
            Label = "Items",
            Id = 5208,
            Address = $"/api/{slug}/5208/tooltip",
        };

        var cut = Render<Identity>(p => p.AddCascadingValue(here));

        Assert.Equal("Address", cut.Find("div.cx-panel-head").TextContent);

        var rows = cut.FindAll("table.cx-facts tr");

        Assert.Equal(3, rows.Count);
        Assert.Equal($"/browse?type={slug}", rows[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Items", rows[0].QuerySelector("a")!.TextContent);
        Assert.Equal("5208", rows[1].QuerySelector("td")!.TextContent);
        Assert.Equal($"/api/{slug}/5208/tooltip", rows[2].QuerySelector("a")!.GetAttribute("href"));
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
            Related = [("Slot: Head", $"/browse?type={EntityTypes.Slug(EntityType.Item)}&slot=Head", 1271)],
        };

        var cut = Render<Nearby>(p => p.AddCascadingValue(here));

        Assert.Equal("More like this", cut.Find("div.cx-panel-head").TextContent);

        var pill = cut.Find("ul.cx-pills a");

        Assert.Equal($"/browse?type={EntityTypes.Slug(EntityType.Item)}&slot=Head", pill.GetAttribute("href"));
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
        var only = new ItemDocument { Id = 1 };
        var here = new Bearings { Shown = only, Readings = [only], Noun = "item" };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Groups_the_readings_into_named_versions_and_lists_who_wears_each()
    {
        var cloth = new ItemDocument
        {
            Id = 1, Name = "Scholar's Headscarf",
            ArmorType = ArmorType.Cloth, PrimaryStat = PrimaryStat.Intellect, Heroes = [Hero.Aeona],
        };
        var leather = new ItemDocument
        {
            Id = 1, Name = "Scribe's Headband",
            ArmorType = ArmorType.Leather, PrimaryStat = PrimaryStat.Agility, Heroes = [Hero.Mara],
        };

        var here = new Bearings
        {
            Shown = cloth,
            Readings = [cloth, leather],
            Reading = "Aeona",
            Page = $"/{EntityTypes.Slug(EntityType.Item)}/1",
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

        Assert.Equal($"/{EntityTypes.Slug(EntityType.Item)}/1?hero=Aeona", wearer.GetAttribute("href"));
        Assert.Equal("page", wearer.GetAttribute("aria-current"));
        Assert.Equal("Aeona", wearer.QuerySelector("span.cx-who")!.TextContent);
    }

    [Fact]
    public void Names_one_version_in_the_singular()
    {
        var reading = new ItemDocument
        {
            Id = 1, Name = "Twin Cowl",
            ArmorType = ArmorType.Cloth, PrimaryStat = PrimaryStat.Intellect, Heroes = [Hero.Aeona],
        };

        var here = new Bearings
        {
            Shown = reading, Readings = [reading, reading],
            Page = $"/{EntityTypes.Slug(EntityType.Item)}/1", Noun = "item",
        };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Equal("1 version", cut.Find("div.cx-panel-head span.cx-num").TextContent);
    }

    [Fact]
    public void Gives_a_version_split_by_hero_alone_no_discriminator()
    {
        var first = new ItemDocument { Id = 1, Name = "Plain Band", Heroes = [Hero.Aeona] };
        var second = new ItemDocument { Id = 1, Name = "Plain Band", Heroes = [Hero.Mara] };

        var here = new Bearings
        {
            Shown = first, Readings = [first, second],
            Page = $"/{EntityTypes.Slug(EntityType.Item)}/1", Noun = "item",
        };

        var cut = Render<Versions>(p => p.AddCascadingValue(here));

        Assert.Single(cut.FindAll("p.cx-sub-head"));
        Assert.Null(cut.Find("p.cx-sub-head").QuerySelector("span.cx-num"));
        Assert.Equal(2, cut.FindAll("div.cx-roster a").Count);
    }
}
