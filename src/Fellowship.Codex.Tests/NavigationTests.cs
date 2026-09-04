using System.Text.RegularExpressions;

namespace Fellowship.Codex.Tests;

public sealed partial class NavigationTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private readonly IMenuService menus = factory.Services.GetRequiredService<IMenuService>();

    private Task<string> Page(string address) =>
        client.GetStringAsync(address, Xunit.TestContext.Current.CancellationToken);

    private static string Masthead(string page) => page.Bare().Split("<main")[0];

    private static string Main(string page) => page.Bare().Split("<main")[1].Split("</main>")[0];

    private static string Menu(string page, string label) =>
        page.Bare().Split($"<summary class=\"cx-menu-open\">{label}</summary>")[1].Split("<summary class=\"cx-menu-open\">")[0];

    [GeneratedRegex("<summary class=\"cx-menu-open\">([^<]+)</summary>")]
    private static partial Regex Opener();

    private const string Row = "<li class=\"cx-menu-row\">";

    private static IEnumerable<string> Rows(string menu)
    {
        var at = 0;

        while (menu.IndexOf(Row, at, StringComparison.Ordinal) is var open && open >= 0)
        {
            var from = open + Row.Length;
            var scan = from;
            var depth = 1;

            while (depth > 0)
            {
                var deeper = menu.IndexOf("<li", scan, StringComparison.Ordinal);
                var shut = menu.IndexOf("</li>", scan, StringComparison.Ordinal);

                if (shut < 0) yield break;

                if (deeper >= 0 && deeper < shut)
                {
                    depth++;
                    scan = deeper + 3;
                    continue;
                }

                depth--;
                scan = shut + 5;

                if (depth == 0)
                {
                    yield return menu[from..shut];
                    at = scan;
                }
            }
        }
    }

    private static string Text(string markup) =>
        string.Join(' ', Regex.Replace(markup, "<[^>]+>", " ").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public async Task The_home_page_carries_the_omnibox_in_main_and_not_in_the_masthead()
    {
        var page = await Page("/");

        Assert.DoesNotContain("cx-omnibox", Masthead(page), StringComparison.Ordinal);
        Assert.Contains("<div class=\"cx-omnibox cx-omnibox-hero\"", Main(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_home_page_carries_the_wordmark_and_the_omnibox_and_nothing_else()
    {
        var main = Main(await Page("/"));

        Assert.Contains("<section class=\"cx-home\">", main, StringComparison.Ordinal);
        Assert.Contains("<h1 class=\"cx-home-name\">Fellowship <b>Codex</b></h1>", main, StringComparison.Ordinal);

        Assert.DoesNotContain("cx-panel", main, StringComparison.Ordinal);
        Assert.DoesNotContain("cx-roster", main, StringComparison.Ordinal);
        Assert.DoesNotContain("cx-card", main, StringComparison.Ordinal);
        Assert.DoesNotContain("entries", main, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Every_other_page_carries_the_omnibox_in_the_masthead()
    {
        foreach (var address in new[] { "/items", "/tools/power-potential", "/item/5250" })
        {
            var page = await Page(address);

            Assert.Contains("<div class=\"cx-omnibox\"", Masthead(page), StringComparison.Ordinal);
            Assert.DoesNotContain("cx-omnibox", Main(page), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_masthead_offers_Database_and_Tools_and_nothing_else()
    {
        var masthead = Masthead(await Page("/items"));

        Assert.Equal(["Database", "Tools"], Opener().Matches(masthead).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public async Task The_masthead_is_the_same_on_the_home_page()
    {
        var masthead = Masthead(await Page("/"));

        Assert.Equal(["Database", "Tools"], Opener().Matches(masthead).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public async Task The_Database_menu_offers_the_categories_and_nothing_beside_them()
    {
        var menu = Menu(await Page("/items"), "Database");

        Assert.Equal(
            [.. Categories.All.Select(category => category.Label)],
            Rows(menu).Select(row => Text(row.Split("</a>")[0])).ToArray());

        foreach (var category in Categories.All)
        {
            Assert.Contains(
                $"<a href=\"/{category.Slug}\">{category.Label}</a>", menu, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Every_link_the_menu_declares_reaches_the_markup()
    {
        var menu = Menu(await Page("/items"), "Database");

        foreach (var link in Declared(menus.Menu))
        {
            Assert.Contains($"href=\"{link.Href.Replace("&", "&amp;")}\"", menu, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_section_heads_the_links_it_holds()
    {
        var menu = Menu(await Page("/items"), "Database");

        var headed = Declared(menus.Menu)
            .SelectMany(link => link.Sections)
            .Where(section => section.Head is not null)
            .ToArray();

        Assert.NotEmpty(headed);

        foreach (var section in headed)
        {
            Assert.Contains($"<p class=\"cx-menu-sec-head\">{section.Head}</p>", menu, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_type_with_no_section_is_a_plain_link()
    {
        var menu = Menu(await Page("/items"), "Database");

        var plain = Declared(menus.Menu).Where(link => link.Sections.Count == 0).ToArray();

        Assert.NotEmpty(plain);

        foreach (var link in plain)
        {
            var anchor = $"<a href=\"{link.Href.Replace("&", "&amp;")}\">{link.Label}</a>";
            var at = menu.IndexOf(anchor, StringComparison.Ordinal);

            Assert.True(at >= 0, $"The menu carries no row for {link.Label}.");
            Assert.StartsWith("</li>", menu[(at + anchor.Length)..].TrimStart(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_menus_carry_no_counts_and_no_school_at_any_level()
    {
        var menu = Menu(await Page("/items"), "Database");

        Assert.DoesNotContain("cx-num", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("School", menu, StringComparison.Ordinal);
        Assert.DoesNotContain("school=", menu, StringComparison.Ordinal);
    }

    private static IEnumerable<MenuLink> Declared(IEnumerable<MenuLink> links) =>
        links.SelectMany(link =>
            Declared(link.Sections.SelectMany(section => section.Links)).Prepend(link));

    [Fact]
    public async Task The_Tools_menu_offers_the_two_tools_and_the_roster_under_the_calculator()
    {
        var menu = Menu(await Page("/items"), "Tools");

        Assert.Contains("<a href=\"/tools/power-potential\">Power Potential</a>", menu, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/tools/talents\">Talent Calculator</a>", menu, StringComparison.Ordinal);

        var rows = Rows(menu).ToArray();

        Assert.Equal(2, rows.Length);

        var calculator = rows.Single(row =>
            row.StartsWith("<a href=\"/tools/talents\">Talent Calculator", StringComparison.Ordinal));

        Assert.Contains("<details class=\"cx-menu-fly\"", calculator, StringComparison.Ordinal);

        foreach (var hero in menus.Roster)
        {
            Assert.Contains(
                $"href=\"/tools/talents?hero={Uri.EscapeDataString(hero)}\"", calculator, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_menus_sit_in_one_drawer_a_toggle_opens()
    {
        var masthead = Masthead(await Page("/items"));

        Assert.Contains(
            "<button type=\"button\" class=\"cx-menus-toggle\" aria-expanded=\"false\" aria-controls=\"cx-menus-drawer\" aria-label=\"Menu\">",
            masthead, StringComparison.Ordinal);
        Assert.Contains("<div class=\"cx-menus-drawer\" id=\"cx-menus-drawer\">", masthead, StringComparison.Ordinal);
        Assert.Contains("<details class=\"cx-menu\" name=\"cx-menu\">", masthead, StringComparison.Ordinal);
    }
}
