namespace FellowshipExtractor.Api.Tests;

public class BorrowedTests : BunitContext
{
    [Fact]
    public void Borrowed_art_renders_nothing_without_a_destination()
    {
        var cut = Render<BorrowedArt>(p => p.Add(x => x.Name, "Old Boots"));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Borrowed_art_names_the_lender_and_carries_its_tip()
    {
        var slug = EntityTypes.Slug(EntityType.Item);
        var cut = Render<BorrowedArt>(p => p
            .Add(x => x.Href, $"/{slug}/12")
            .Add(x => x.Tip, $"/api/{slug}/12/tooltip")
            .Add(x => x.Name, "Old Boots"));

        Assert.Equal("Borrowed art", cut.Find("div.cx-panel-head").TextContent);

        var link = cut.Find("p.cx-note a");

        Assert.Equal($"/{slug}/12", link.GetAttribute("href"));
        Assert.Equal($"/api/{slug}/12/tooltip", link.GetAttribute("data-tip"));
        Assert.Equal("Old Boots", link.TextContent);
    }

    [Fact]
    public void Borrowed_name_renders_nothing_without_a_destination()
    {
        var cut = Render<BorrowedName>(p => p.Add(x => x.Name, "Sunder"));

        Assert.Empty(cut.Markup);
    }

    [Fact]
    public void Borrowed_name_names_the_entry_that_applies_this_one()
    {
        var slug = EntityTypes.Slug(EntityType.Ability);
        var cut = Render<BorrowedName>(p => p
            .Add(x => x.Href, $"/{slug}/9")
            .Add(x => x.Tip, $"/api/{slug}/9/tooltip")
            .Add(x => x.Name, "Sunder"));

        Assert.Equal("Borrowed name", cut.Find("div.cx-panel-head").TextContent);
        Assert.Equal("Sunder", cut.Find("p.cx-note a").TextContent);
    }
}
