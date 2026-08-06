namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class DetailTests : BunitContext
{
    public DetailTests(Export export) => Services.AddSingleton(export.View);

    [Fact]
    public void A_nested_detail_draws_the_granted_ability_and_not_the_record_that_grants_it()
    {
        var granted = new Entity
        {
            Description = "the granted sentence",
            Stats = [new StatSlot("StatHaste", 15) { Kind = "Secondary" }],
            RankRuns = [new RankRun("7", ["7"])],
        };

        var outer = new Entity
        {
            Description = "the outer sentence",
            Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }],
            Grants = granted,
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, outer));
        var markup = cut.Markup;

        var panel = cut.FindAll("div.cx-panel")
            .Single(e => e.QuerySelector("div.cx-panel-head")?.TextContent.StartsWith("Grants an ability") == true);

        Assert.Contains("the granted sentence", panel.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("the outer sentence", panel.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Stamina", panel.TextContent, StringComparison.Ordinal);

        Assert.Equal(1, Occurrences(markup, "the outer sentence"));
        Assert.Equal(1, Occurrences(markup, "Grants an ability"));
    }

    [Fact]
    public void A_detail_re_rendered_with_a_different_entity_draws_the_new_one()
    {
        var first = new Entity { Description = "the first sentence" };
        var second = new Entity { Description = "the second sentence" };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, first));

        Assert.Contains("the first sentence", cut.Markup, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Entity, second));

        Assert.Contains("the second sentence", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("the first sentence", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_legendary_roll_draws_its_own_text_under_its_own_sub_head()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { Description = "the first roll" },
                new Entity { Description = "the second roll" },
            ],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        Assert.Equal(2, cut.FindAll("p.cx-sub-head").Count);
        Assert.Contains("the first roll", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("the second roll", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_structurally_equal_legendary_rolls_survive_a_re_render()
    {
        var entity = new Entity
        {
            LegendaryStats =
            [
                new Entity { Description = "the same words" },
                new Entity { Description = "the same words" },
            ],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        cut.Render(p => p.Add(x => x.Entity, entity with { }));

        Assert.Equal(2, cut.FindAll("p.cx-sub-head").Count);
    }

    [Fact]
    public void The_panels_are_siblings_with_no_wrapper_around_them()
    {
        var entity = new Entity
        {
            Description = "some words",
            Stats = [new StatSlot("Stamina", 40) { Kind = "Primary" }],
        };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, entity));

        var roots = cut.Nodes.OfType<IElement>().ToList();

        Assert.Equal(2, roots.Count);
        Assert.All(roots, root => Assert.Contains("cx-panel", root.ClassList));
    }

    [Fact]
    public void A_nested_row_builds_its_school_filter_without_a_type()
    {
        var granted = new Entity { Tags = ["MagicFire"] };
        var outer = new Entity { Table = "GameplayAbility", Tags = ["MagicFire"], Grants = granted };

        var cut = Render<Detail>(p => p.Add(x => x.Entity, outer));

        var hrefs = cut.FindAll("ul.cx-pills a")
            .Select(a => a.GetAttribute("href"))
            .Where(href => href?.Contains("school=") == true)
            .ToList();

        Assert.Contains(hrefs, href => href!.StartsWith("/browse?type=", StringComparison.Ordinal));
        Assert.Contains(hrefs, href => href!.StartsWith("/browse?school=", StringComparison.Ordinal));
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
