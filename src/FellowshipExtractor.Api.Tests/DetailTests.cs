using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The four hazards the cascade carries. Each of them compiles cleanly when broken and shows only
/// as the wrong record on screen, which is the reason this project exists.
///
/// Grants and Legendaries resolve their nested rows against the store rather than nesting a bare
/// record, so the two tests exercising them read real corpus rows instead of hand-built fixtures:
/// item 5246 (Humming Portalstone) grants relic power 432 (Conjure Portal), and item 5216 carries
/// the three legendary stat ids 696, 695 and 694.
/// </summary>
[Collection(Corpus.Name)]
public class DetailTests : BunitContext
{
    public DetailTests(Export export)
    {
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Tooltip);
    }

    /// <summary>
    /// A single cascade declared on the page would flow past the nested Detail unchanged, so the
    /// granted power's panels would draw the outer item's text and stats. The outer Grants guard
    /// would also still be truthy inside, and the recursion would only stop when the renderer said
    /// so - which is what the "exactly once" assertion catches.
    /// </summary>
    [Fact]
    public void A_nested_detail_draws_the_granted_power_and_not_the_record_that_grants_it()
    {
        var outer = new Item { Id = 5246, GrantsId = 432 };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, outer));
        var markup = cut.Markup;

        var panel = cut.FindAll("div.cx-panel")
            .Single(e => e.QuerySelector("div.cx-panel-head")?.TextContent.StartsWith("Grants an ability") == true);

        Assert.Contains("Conjures two interactable", panel.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Agility", panel.TextContent, StringComparison.Ordinal);
        Assert.Empty(panel.QuerySelectorAll("ul.cx-stat-lines"));

        Assert.Equal(1, Occurrences(markup, "Grants an ability"));
    }

    /// <summary>
    /// This is what holds IsFixed off the cascade. With IsFixed="true" a re-rendered Detail keeps
    /// drawing the first entity, measured, and nothing else in the suite notices.
    /// </summary>
    [Fact]
    public void A_detail_re_rendered_with_a_different_entity_draws_the_new_one()
    {
        var first = new Ability { Id = 1, Description = "the first sentence" };
        var second = new Ability { Id = 2, Description = "the second sentence" };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, first));

        Assert.Contains("the first sentence", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Entity, second));

        Assert.Contains("the second sentence", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("the first sentence", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every legendary roll draws under its own sub-head, in the order
    /// <see cref="Item.LegendaryStatIds"/> lists them, and the loop's
    /// <c>@@key="index"</c> keeps that true across a re-render.
    ///
    /// Each roll's own nested Detail draws its own Particulars panel too, and all three of these
    /// rolls carry a ScalesOff list, so each nested Detail contributes its own "Scales off" sub-head
    /// beside the roll's name one. Both panels emit no wrapper, so the nested one lands as a sibling
    /// div rather than inside the roll-name sub-head; direct children of the Legendary stats panel is
    /// what tells the two kinds of sub-head apart.
    /// </summary>
    [Fact]
    public void Each_legendary_roll_draws_its_own_text_under_its_own_sub_head_and_survives_a_re_render()
    {
        var entity = new Item { Id = 5216, LegendaryStatIds = [696, 695, 694] };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        IElement[] RollHeads()
        {
            var panel = cut.FindAll("div.cx-panel")
                .Single(e => e.QuerySelector(":scope > div.cx-panel-head")?.TextContent.StartsWith("Legendary stats") == true);

            return [.. panel.Children.Where(c => c.TagName == "P" && c.ClassList.Contains("cx-sub-head"))];
        }

        var heads = RollHeads();

        Assert.Equal(3, heads.Length);
        Assert.Equal(["Strand of Eternity", "Blood of the Undying", "Mote of Creation"],
            heads.Select(head => head.TextContent));
        Assert.Contains("+10% Cooldown Acceleration", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("+10 Damage Reduction", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("+10% Primary Stat", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Entity, entity));

        Assert.Equal(3, RollHeads().Length);
    }

    /// <summary>
    /// CascadingValue emits no element, so the panels are siblings at the component root and
    /// .cx-panel + .cx-panel is what supplies the gap between them. A wrapper element here would
    /// leave every panel on the page flush against the next.
    /// </summary>
    [Fact]
    public void The_panels_are_siblings_with_no_wrapper_around_them()
    {
        var entity = new Item
        {
            Id = 1,
            Description = "some words",
            Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        var roots = cut.Nodes.OfType<IElement>().ToList();

        Assert.Equal(2, roots.Count);
        Assert.All(roots, root => Assert.Contains("cx-panel", root.ClassList));
    }

    /// <summary>
    /// Every document is a real, separately-typed row now, nested or not, so its school pill links
    /// with a type filter unconditionally: there is no longer a "this row has no table of its own"
    /// branch to fall back from.
    /// </summary>
    [Fact]
    public void A_rows_school_filter_carries_its_own_type()
    {
        var entity = new Ability { Id = 1, Schools = ["Magic / Fire"] };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        var hrefs = cut.FindAll("ul.cx-pills a")
            .Select(a => a.GetAttribute("href"))
            .Where(href => href?.Contains("school=") == true)
            .ToList();

        var expected = $"/browse?type={EntityTypes.Slug(EntityType.Ability)}&school={Uri.EscapeDataString("Magic / Fire")}";

        Assert.Contains(hrefs, href => href == expected);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;

        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
