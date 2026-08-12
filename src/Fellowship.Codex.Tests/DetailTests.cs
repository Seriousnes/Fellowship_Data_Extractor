namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class DetailTests : BunitContext
{
    public DetailTests(Export export)
    {
        Services.AddSingleton(export.View);
        Services.AddSingleton(export.Tooltip);
    }

    [Fact]
    public void A_nested_detail_draws_the_granted_power_and_not_the_record_that_grants_it()
    {
        var outer = new ItemDocument { Id = 5246, Grants = new Fellowship.SDK.Models.EntityReference(EntityType.RelicPower, 432, "Conjure Portal") };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, outer));
        var markup = cut.Markup;

        var panel = cut.FindAll("div.cx-panel")
            .Single(e => e.QuerySelector("div.cx-panel-head")?.TextContent.StartsWith("Grants an ability") == true);

        Assert.Contains("Conjures two interactable", panel.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Agility", panel.TextContent, StringComparison.Ordinal);
        Assert.Empty(panel.QuerySelectorAll("ul.cx-stat-lines"));

        Assert.Equal(1, Occurrences(markup, "Grants an ability"));
    }

    [Fact]
    public void A_detail_re_rendered_with_a_different_entity_draws_the_new_one()
    {
        var first = new AbilityDocument { Id = 1, Description = "the first sentence" };
        var second = new AbilityDocument { Id = 2, Description = "the second sentence" };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, first));

        Assert.Contains("the first sentence", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Entity, second));

        Assert.Contains("the second sentence", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("the first sentence", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_legendary_roll_draws_its_own_text_under_its_own_sub_head_and_survives_a_re_render()
    {
        var entity = new ItemDocument { Id = 5216, LegendaryStats = [.. new[] { 696, 695, 694 }.Select(id => new Fellowship.SDK.Models.EntityReference(EntityType.LegendaryStat, id, null))] };

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

    [Fact]
    public void The_panels_are_siblings_with_no_wrapper_around_them()
    {
        var entity = new ItemDocument
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

    [Fact]
    public void A_rows_school_filter_carries_its_own_type()
    {
        var entity = new AbilityDocument { Id = 1, Schools = ["Magic / Fire"] };

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
