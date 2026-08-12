using System.Text;
using System.Text.RegularExpressions;

namespace Fellowship.Codex.Tests;

public class BrowsePageTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private Task<string> Browse(string query = "") =>
        client.GetStringAsync("/browse" + query, Xunit.TestContext.Current.CancellationToken);

    private static string[] Heads(string page) =>
        [.. page.Bare().Split("<thead>")[1].Split("</thead>")[0]
            .Split("<th")
            .Skip(1)
            .Select(cell => Stripped(cell.Split('>', 2)[1].Split("</th>")[0]))];

    private static string[] Tabs(string page) =>
        [.. page.Bare().Split("<a class=\"cx-tab\"").Skip(1).Select(tab => Stripped(tab.Split("</a>")[0]))];

    private static string Current(string page) =>
        Stripped(page.Bare().Split("aria-current=\"page\"")[1].Split("</a>")[0]);

    private static string Stripped(string cell)
    {
        var text = new StringBuilder(cell.Length);
        var inside = false;

        foreach (var letter in cell)
        {
            if (letter == '<') inside = true;
            else if (letter == '>') inside = false;
            else if (!inside) text.Append(letter);
        }

        return string.Join(' ', text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task A_bare_browse_draws_the_items_list_and_marks_the_items_tab_current()
    {
        var page = await Browse();

        Assert.Equal(["", "Name", "ILvl", "Slot", "Source", "Type", "Set"], Heads(page));
        Assert.StartsWith(PublicTypes.Label(EntityType.Item), Current(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bare_browse_heads_the_type_it_draws()
    {
        var page = (await Browse()).Bare();

        Assert.Contains($"<h1>{PublicTypes.Label(EntityType.Item)}</h1>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<h1>Browse the codex</h1>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_query_naming_a_type_draws_that_types_list()
    {
        var page = await Browse("?type=" + EntityTypes.Slug(EntityType.SetBonus));

        Assert.Equal(["", "Name", "Required", "Attribute", "Role"], Heads(page));
        Assert.StartsWith(PublicTypes.Label(EntityType.SetBonus), Current(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_untabbed_type_still_draws_its_list_and_marks_itself_current()
    {
        var page = await Browse("?type=" + EntityTypes.Slug(EntityType.Gem));

        Assert.StartsWith(PublicTypes.Label(EntityType.Gem), Current(page), StringComparison.Ordinal);
        Assert.Equal("Name", Heads(page)[1]);
    }

    [Fact]
    public async Task A_query_matching_only_abilities_draws_the_abilities_list()
    {
        var name = NameNoOtherTypeCarries();

        var carrying = RouteTests.Factory.Store.Entities
            .Where(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(carrying);
        Assert.All(carrying, row => Assert.IsType<AbilityDocument>(row));

        var page = await Browse("?q=" + Uri.EscapeDataString(name));

        Assert.Equal(["", "Name", "Source", "Type"], Heads(page));
        Assert.StartsWith(PublicTypes.Label(EntityType.Ability), Current(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_search_heads_its_own_words_rather_than_the_type_it_lands_on()
    {
        var name = NameNoOtherTypeCarries();

        var page = (await Browse("?q=" + Uri.EscapeDataString(name))).Bare();

        Assert.Contains("Search results for", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"<h1>{PublicTypes.Label(EntityType.Ability)}</h1>", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_page_draws_a_tab_labelled_all()
    {
        foreach (var query in new[] { "", "?type=" + EntityTypes.Slug(EntityType.Ability), "?q=shield" })
        {
            var tabs = Tabs(await Browse(query));

            Assert.NotEmpty(tabs);
            Assert.DoesNotContain(tabs, tab => tab.StartsWith("All", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task The_table_the_page_draws_carries_the_scope_its_chrome_is_written_under()
    {
        var css = File.ReadAllText(Project.Bundle);
        var scope = Regex.Match(css, @"table\.cx-rows\[b-([a-z0-9]{10})\]");

        Assert.True(scope.Success, "The bundle carries no scoped table.cx-rows selector.");

        var page = await Browse();

        Assert.Contains($"b-{scope.Groups[1].Value}", page, StringComparison.Ordinal);
        Assert.Matches($@"<table class=""cx-rows"" b-{scope.Groups[1].Value}", page);
    }

    private static string NameNoOtherTypeCarries()
    {
        var store = RouteTests.Factory.Store;

        var elsewhere = store.Entities
            .Where(row => row is not AbilityDocument && row.Name is { Length: > 0 })
            .SelectMany(row => Words(row.Name!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return store.Entities.OfType<AbilityDocument>()
            .Where(row => row.Name is { Length: > 0 })
            .Select(row => row.Name!)
            .First(name => Words(name) is { Length: > 0 } words && words.All(word => !elsewhere.Contains(word)));
    }

    private static string[] Words(string text) =>
        [.. text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim('\'', ',', ':', '.', '(', ')'))
            .Where(word => word.Length > 0)];
}
