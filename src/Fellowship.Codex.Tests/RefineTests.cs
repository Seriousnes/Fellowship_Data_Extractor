using System.Text.RegularExpressions;

namespace Fellowship.Codex.Tests;

public sealed partial class RefineTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private readonly ICodexService codex = factory.Services.GetRequiredService<ICodexService>();

    private Task<string> Page(string query) =>
        client.GetStringAsync(query, Xunit.TestContext.Current.CancellationToken);

    private static string Main(string page) => page.Bare().Split("<main")[1].Split("</main>")[0];

    private static string Rail(string page) =>
        page.Bare().Split("<aside class=\"cx-panel cx-rail cx-panel-collapsible\" aria-label=\"Filters\">")[1].Split("</aside>")[0];

    private static string Row(string rail, string key)
    {
        var at = rail.IndexOf($"data-more=\"{key}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"The rail carries no row keyed {key}.");

        var start = rail.LastIndexOf("<details", at, StringComparison.Ordinal);
        var end = rail.IndexOf("</details>", at, StringComparison.Ordinal);

        return rail[start..end];
    }

    private static string Select(string rail) =>
        rail.Split("<select class=\"cx-more\" aria-label=\"Additional filters\">")[1].Split("</select>")[0];

    [GeneratedRegex("<details class=\"cx-facet\" data-more=\"([a-z-]+)\" hidden open>")]
    private static partial Regex HiddenRow();

    [GeneratedRegex("<option value=\"([a-z-]+)\">")]
    private static partial Regex Option();

    private static string Items => PublicTypes.Where(EntityType.Item);

    private static string Abilities => PublicTypes.Where(EntityType.Ability);

    [Fact]
    public async Task Items_keep_slot_and_rarity_in_view_and_hold_every_other_facet_behind_the_select()
    {
        var rail = Rail(await Page(Items));

        Assert.Contains("<details class=\"cx-facet\" open><summary>Slot</summary>", rail, StringComparison.Ordinal);
        Assert.Contains("<details class=\"cx-facet\" open><summary>Rarity</summary>", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("data-more=\"facet-slot\"", rail, StringComparison.Ordinal);
        Assert.DoesNotContain("data-more=\"facet-rarity\"", rail, StringComparison.Ordinal);

        foreach (var key in new[] { "armor", "stat", "source", "set" })
        {
            var row = Row(rail, "facet-" + key);
            var label = codex.Facet(key)!.Label;

            Assert.StartsWith($"<details class=\"cx-facet\" data-more=\"facet-{key}\" hidden open>", row, StringComparison.Ordinal);
            Assert.Contains($"<summary>{label} <button type=\"button\" class=\"cx-facet-off\">remove</button></summary>", row, StringComparison.Ordinal);
            Assert.Contains($"<option value=\"facet-{key}\">{label}</option>", Select(rail), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Every_primary_facet_the_codex_declares_stays_out_of_the_select()
    {
        var primary = codex.Facets.Where(facet => facet.Primary).Select(facet => "facet-" + facet.Key).ToArray();

        Assert.NotEmpty(primary);

        foreach (var query in new[] { Items, Abilities, PublicTypes.Where(EntityType.Gem) })
        {
            var rail = Rail(await Page(query));

            foreach (var key in primary)
            {
                Assert.DoesNotContain($"data-more=\"{key}\"", rail, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task The_select_lists_every_hidden_row_and_nothing_else()
    {
        foreach (var query in new[] { Items, Abilities })
        {
            var rail = Rail(await Page(query));

            var hidden = HiddenRow().Matches(rail).Select(match => match.Groups[1].Value).ToArray();
            var offered = Option().Matches(Select(rail)).Select(match => match.Groups[1].Value).ToArray();

            Assert.NotEmpty(hidden);
            Assert.Equal(hidden, offered);
        }

        var select = Select(Rail(await Page(Abilities)));

        Assert.Contains("<option value>Additional filters…</option>", select, StringComparison.Ordinal);
        Assert.Contains("<optgroup label=\"Filter by\">", select, StringComparison.Ordinal);
        Assert.Contains("<optgroup label=\"Only rows that\">", select, StringComparison.Ordinal);
        Assert.Contains("<optgroup label=\"Between\">", select, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_flag_no_row_of_the_type_meets_is_absent_from_the_rail()
    {
        var abilities = Rail(await Page(Abilities));
        var items = Rail(await Page(Items));

        Assert.DoesNotContain("flag-socket", abilities, StringComparison.Ordinal);
        Assert.DoesNotContain("flag-scaling", items, StringComparison.Ordinal);
        Assert.Contains("data-more=\"flag-scaling\"", abilities, StringComparison.Ordinal);
        Assert.Contains("data-more=\"flag-socket\"", items, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unstated_flag_row_hides_and_offers_yes_and_no()
    {
        var row = Row(Rail(await Page(Items)), "flag-icon");

        Assert.StartsWith("<details class=\"cx-facet\" data-more=\"flag-icon\" hidden open>", row, StringComparison.Ordinal);
        Assert.Contains("<summary>Has art <button type=\"button\" class=\"cx-facet-off\">remove</button></summary>", row, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"/items?type=item&amp;icon=1\">Yes</a></li>", row, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"/items?type=item&amp;icon=0\">No</a></li>", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stated_flag_draws_its_row_open_marks_its_answer_and_links_its_removal()
    {
        var rail = Rail(await Page(Items + "&socket=1"));
        var row = Row(rail, "flag-socket");

        Assert.StartsWith("<details class=\"cx-facet\" data-more=\"flag-socket\" open>", row, StringComparison.Ordinal);
        Assert.Contains("<summary>Rolls with a gem socket <a class=\"cx-facet-off\" href=\"/items?type=item\">remove</a></summary>", row, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"/items?type=item&amp;socket=1\" class=\"on\">Yes</a></li>", row, StringComparison.Ordinal);
        Assert.Contains("<li><a href=\"/items?type=item&amp;socket=0\">No</a></li>", row, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"flag-socket\"", Select(rail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_chosen_additional_facet_draws_its_row_open_and_links_its_removal()
    {
        var armor = codex.Offered(codex.Facet("armor")!)[0];
        var rail = Rail(await Page(Items + "&armor=" + Uri.EscapeDataString(armor)));
        var row = Row(rail, "facet-armor");

        Assert.StartsWith("<details class=\"cx-facet\" data-more=\"facet-armor\" open>", row, StringComparison.Ordinal);
        Assert.Contains("<summary>Armour <a class=\"cx-facet-off\" href=\"/items?type=item\">remove</a></summary>", row, StringComparison.Ordinal);
        Assert.Contains("class=\"on\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"facet-armor\"", Select(rail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bounded_measure_draws_its_row_open_inside_the_form_and_links_its_removal()
    {
        var rail = Rail(await Page(Abilities + "&cooldownMin=10"));
        var form = rail.Split("<form class=\"cx-ranges\" method=\"get\" action=\"/spells\">")[1].Split("</form>")[0];
        var row = Row(form, "range-cooldown");

        Assert.StartsWith("<details class=\"cx-facet\" data-more=\"range-cooldown\" open>", row, StringComparison.Ordinal);
        Assert.Contains("<summary>Cooldown (s) <a class=\"cx-facet-off\" href=\"/spells?type=ability\">remove</a></summary>", row, StringComparison.Ordinal);
        Assert.Contains("name=\"cooldownMin\"", row, StringComparison.Ordinal);
        Assert.Contains("value=\"10\"", row, StringComparison.Ordinal);
        Assert.StartsWith("<details class=\"cx-facet\" data-more=\"range-cast\" hidden open>", Row(form, "range-cast"), StringComparison.Ordinal);
        Assert.Contains("<button type=\"submit\">Apply</button>", form, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"range-cooldown\"", Select(rail), StringComparison.Ordinal);
        Assert.Contains("<option value=\"range-cast\">Cast time (s)</option>", Select(rail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Grouping_stays_closed_until_the_query_groups()
    {
        Assert.Contains("<details class=\"cx-facet cx-group\"><summary>Group results by</summary>", Rail(await Page(Items)), StringComparison.Ordinal);
        Assert.Contains("<details class=\"cx-facet cx-group\" open><summary>Group results by</summary>", Rail(await Page(Items + "&group=slot")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rail_precedes_the_results_and_its_head_folds_on_a_phone()
    {
        var main = Main(await Page(Items + "&socket=1"));

        Assert.True(
            main.IndexOf("<aside class=\"cx-panel cx-rail", StringComparison.Ordinal) <
            main.IndexOf("<nav class=\"cx-pager\"", StringComparison.Ordinal));

        Assert.Contains("<div class=\"cx-panel-head\">Refine<span class=\"cx-num\">2</span></div>", main, StringComparison.Ordinal);
        Assert.Contains("<button type=\"button\" class=\"cx-panel-head cx-panel-toggle\" aria-expanded=\"false\">Refine<span class=\"cx-num\">2</span></button>", main, StringComparison.Ordinal);
        Assert.Contains("<div class=\"cx-panel-body\">", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rail_counts_no_rows_and_keeps_school_behind_the_select()
    {
        var rail = Rail(await Page(Abilities));
        var body = rail.Split("<div class=\"cx-panel-body\">")[1];

        Assert.DoesNotContain("cx-num", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<details class=\"cx-facet\" open><summary>School</summary>", rail, StringComparison.Ordinal);
        Assert.Contains("data-more=\"facet-school\"", rail, StringComparison.Ordinal);
        Assert.Contains("<option value=\"facet-school\">School</option>", Select(rail), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_page_counts_its_rows_beside_the_pager_at_the_top_and_the_foot()
    {
        var main = Main(await Page(Items));

        var pagers = main.Split("<nav class=\"cx-pager\" aria-label=\"Pagination\">").Length - 1;

        Assert.Equal(2, pagers);
        Assert.Contains("</b> entities", main, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/items?type=item&amp;page=2\">Next", main, StringComparison.Ordinal);

        var second = Main(await Page(Items + "&page=2"));

        Assert.Contains("First</a>", second, StringComparison.Ordinal);
        Assert.Contains("Prev</a>", second, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_category_with_no_type_draws_every_type_in_one_table_under_an_All_tab()
    {
        var main = Main(await Page("/dungeons"));

        Assert.Contains("<a class=\"cx-tab\" href=\"/dungeons\" aria-current=\"page\">", main, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/dungeons?sort=type\">Type</a>", main, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/dungeons?type=dungeon\">", main, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/dungeons?type=creature\">", main, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_a_search_counts_its_types_on_the_tabs()
    {
        Assert.DoesNotContain("cx-num", Main(await Page("/items")), StringComparison.Ordinal);
        Assert.Contains("cx-num", Main(await Page("/search?q=ancient")), StringComparison.Ordinal);
    }
}
