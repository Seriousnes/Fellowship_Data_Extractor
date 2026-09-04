using System.Text.RegularExpressions;

namespace Fellowship.Codex.Tests;

public sealed partial class NavigationTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private readonly ICodexService codex = factory.Services.GetRequiredService<ICodexService>();

    private Task<string> Page(string address) =>
        client.GetStringAsync(address, Xunit.TestContext.Current.CancellationToken);

    private static string Masthead(string page) => page.Bare().Split("<main")[0];

    private static string Main(string page) => page.Bare().Split("<main")[1].Split("</main>")[0];

    private static string Menu(string page, string label) =>
        page.Bare().Split($"<summary class=\"cx-menu-open\">{label}</summary>")[1].Split("<summary class=\"cx-menu-open\">")[0];

    [GeneratedRegex("<summary class=\"cx-menu-open\">([^<]+)</summary>")]
    private static partial Regex Opener();

    private static IEnumerable<string> Rows(string menu) =>
        menu.Split("<li class=\"cx-menu-row\">").Skip(1).Select(chunk => chunk[..chunk.LastIndexOf("</li>", StringComparison.Ordinal)]);

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
        foreach (var address in new[] { "/browse", "/tools/power-potential", "/item/5250" })
        {
            var page = await Page(address);

            Assert.Contains("<div class=\"cx-omnibox\"", Masthead(page), StringComparison.Ordinal);
            Assert.DoesNotContain("cx-omnibox", Main(page), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_masthead_offers_Database_and_Tools_and_nothing_else()
    {
        var masthead = Masthead(await Page("/browse"));

        Assert.Equal(["Database", "Tools"], Opener().Matches(masthead).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public async Task The_masthead_is_the_same_on_the_home_page()
    {
        var masthead = Masthead(await Page("/"));

        Assert.Equal(["Database", "Tools"], Opener().Matches(masthead).Select(match => match.Groups[1].Value));
    }

    [Fact]
    public async Task The_Database_menu_lists_every_listed_type_under_its_group()
    {
        var menu = Menu(await Page("/browse"), "Database");

        foreach (var group in codex.Menu)
        {
            Assert.Contains($"<h2 class=\"cx-menu-group-head\">{group.Label}</h2>", menu, StringComparison.Ordinal);

            foreach (var entry in group.Entries)
            {
                Assert.Contains($"<a href=\"/browse{entry.Query}\">{entry.Label}</a>", menu, StringComparison.Ordinal);
            }
        }

        foreach (var type in PublicTypes.All.Where(type => type.Listed))
        {
            Assert.Contains($"href=\"/browse?type={type.Slug}\"", menu, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_type_row_carries_no_count()
    {
        var menu = Menu(await Page("/browse"), "Database");

        foreach (var row in Rows(menu))
        {
            var label = Text(row.Split("</a>")[0]);

            Assert.DoesNotContain(label, char.IsDigit);
        }
    }

    [Fact]
    public async Task A_type_with_sections_opens_a_flyout_naming_them_and_their_values()
    {
        var menu = Menu(await Page("/browse"), "Database");

        var sectioned = codex.Menu.SelectMany(group => group.Entries).Where(entry => entry.Sections.Count > 0).ToArray();
        Assert.NotEmpty(sectioned);

        foreach (var entry in sectioned)
        {
            var row = Rows(menu).Single(row => row.Contains($"<a href=\"/browse{entry.Query}\">", StringComparison.Ordinal));

            Assert.Contains("<details class=\"cx-menu-fly\"", row, StringComparison.Ordinal);
            Assert.Contains($"aria-label=\"{entry.Label} categories\"", row, StringComparison.Ordinal);

            foreach (var section in entry.Sections)
            {
                Assert.Contains($"<p class=\"cx-menu-sec-head\">{section.Label}</p>", row, StringComparison.Ordinal);

                foreach (var link in section.Links)
                {
                    Assert.Contains($"href=\"/browse{link.Query.Replace("&", "&amp;")}\"", row, StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public async Task A_type_without_sections_is_a_plain_link()
    {
        var menu = Menu(await Page("/browse"), "Database");

        var plain = codex.Menu.SelectMany(group => group.Entries).Where(entry => entry.Sections.Count == 0).ToArray();
        Assert.NotEmpty(plain);

        foreach (var entry in plain)
        {
            var row = Rows(menu).Single(row => row.Contains($"<a href=\"/browse{entry.Query}\">", StringComparison.Ordinal));

            Assert.DoesNotContain("<details", row, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_Tools_menu_offers_the_two_tools_and_the_roster_under_the_calculator()
    {
        var menu = Menu(await Page("/browse"), "Tools");
        var store = factory.Services.GetRequiredService<IEntityStoreService>();

        Assert.Contains("<a href=\"/tools/power-potential\">Power Potential</a>", menu, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/tools/talents\">Talent Calculator</a>", menu, StringComparison.Ordinal);

        var rows = Rows(menu).ToArray();
        Assert.Equal(2, rows.Length);

        var calculator = rows.Single(row => row.Contains("/tools/talents\">Talent Calculator", StringComparison.Ordinal));

        Assert.Contains("<details class=\"cx-menu-fly\"", calculator, StringComparison.Ordinal);

        foreach (var hero in store.Settings.Heroes)
        {
            Assert.Contains(
                $"href=\"/tools/talents?hero={Uri.EscapeDataString(hero.Name)}\"", calculator, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_menus_sit_in_one_drawer_a_toggle_opens()
    {
        var masthead = Masthead(await Page("/browse"));

        Assert.Contains(
            "<button type=\"button\" class=\"cx-menus-toggle\" aria-expanded=\"false\" aria-controls=\"cx-menus-drawer\" aria-label=\"Menu\">",
            masthead, StringComparison.Ordinal);
        Assert.Contains("<div class=\"cx-menus-drawer\" id=\"cx-menus-drawer\">", masthead, StringComparison.Ordinal);
        Assert.Contains("<details class=\"cx-menu\" name=\"cx-menu\">", masthead, StringComparison.Ordinal);
    }
}
