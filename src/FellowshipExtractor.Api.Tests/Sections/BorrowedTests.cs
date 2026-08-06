namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// BorrowedArt and BorrowedName are the same shape and are tested together: both take three
/// strings, both disappear on a null Href, and both carry the whole of their prose in a note.
/// </summary>
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
            .Add(x => x.Href, $"/entity/{slug}/12")
            .Add(x => x.Tip, $"/api/tooltip/{slug}/12")
            .Add(x => x.Name, "Old Boots"));

        Assert.Equal("Borrowed art", cut.Find("div.cx-panel-head").TextContent);

        var link = cut.Find("p.cx-note a");

        Assert.Equal($"/entity/{slug}/12", link.GetAttribute("href"));
        Assert.Equal($"/api/tooltip/{slug}/12", link.GetAttribute("data-tip"));
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
            .Add(x => x.Href, $"/entity/{slug}/9")
            .Add(x => x.Tip, $"/api/tooltip/{slug}/9")
            .Add(x => x.Name, "Sunder"));

        Assert.Equal("Borrowed name", cut.Find("div.cx-panel-head").TextContent);
        Assert.Equal("Sunder", cut.Find("p.cx-note a").TextContent);
    }
}
