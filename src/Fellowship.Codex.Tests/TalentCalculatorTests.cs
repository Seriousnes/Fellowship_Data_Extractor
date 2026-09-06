using System.Text.RegularExpressions;

using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public partial class TalentCalculatorTests(RouteTests.Factory factory) : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client = factory.CreateClient();

    private static readonly IEntityStoreService Store = RouteTests.Factory.Store;

    private const string Page = "/tools/talents";

    private Task<string> Draw(string query = "") =>
        client.GetStringAsync(Page + query, Xunit.TestContext.Current.CancellationToken);

    [GeneratedRegex("<(a|span) class=\"cx-tal\"[^>]*>")]
    private static partial Regex Cells();

    [GeneratedRegex("<span class=\"cx-tal-row\"[^>]*>([^<]+)</span>")]
    private static partial Regex Rows();

    [GeneratedRegex("<span class=\"cx-tal-name\"[^>]*>([^<]+)</span>")]
    private static partial Regex Names();

    [GeneratedRegex("<div class=\"cx-roster\">.*?</div>", RegexOptions.Singleline)]
    private static partial Regex Picker();

    [GeneratedRegex(
        "<(a|span) class=\"cx-tal\"[^>]*>\\s*<span class=\"cx-tal-face\" data-tip=\"[^\"]*\">" +
        "\\s*<span class=\"cx-tal-art\" style=\"[^\"]*\"",
        RegexOptions.Singleline)]
    private static partial Regex Faces();

    private static string[] Whole(string page) =>
        [.. Faces().Matches(page.Bare()).Select(cell => cell.Value)];

    private static string[] Numerals(string page) =>
        [.. Rows().Matches(page.Bare()).Select(row => row.Groups[1].Value)];

    private static string[] Cell(string page) => [.. Cells().Matches(page.Bare()).Select(cell => cell.Value)];

    private static string[] Named(string page) =>
        [.. Names().Matches(page.Bare()).Select(name => System.Net.WebUtility.HtmlDecode(name.Groups[1].Value))];

    private static string Roster(string page) => Picker().Match(page.Bare()).Value;

    private static Hero First => Enum.GetValues<Hero>().Order().First();

    private static IReadOnlyList<TalentRow> Tree(Hero hero) => TalentPlan.Tree(Store, hero);

    private static string Asking(Hero hero) => $"?hero={Uri.EscapeDataString(hero.ToString())}";

    [Fact]
    public async Task A_bare_calculator_offers_every_hero_and_draws_no_tree()
    {
        var page = await Draw();

        foreach (var hero in Store.Settings.Heroes)
        {
            Assert.Contains($">{hero.Name}</span>", page, StringComparison.Ordinal);
        }

        Assert.Empty(Cell(page));
        Assert.Contains("No hero picked.", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bare_calculator_marks_no_hero_current()
    {
        var page = await Draw();

        Assert.NotEmpty(Roster(page));
        Assert.DoesNotContain("aria-current", Roster(page), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_picked_hero_is_the_only_one_the_picker_marks_current()
    {
        var hero = First;

        var roster = Roster(await Draw(Asking(hero)));

        Assert.Equal(1, Regex.Count(roster, "aria-current=\"page\""));
        Assert.Contains($"aria-current=\"page\"", roster, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_picked_hero_draws_one_cell_per_slot_under_a_roman_numeral_per_row()
    {
        var hero = First;
        var tree = Tree(hero);

        var page = await Draw(Asking(hero));

        Assert.Equal(tree.Sum(row => row.Nodes.Count), Cell(page).Length);
        Assert.Equal([.. tree.Select(row => row.Numeral)], Numerals(page));
    }

    [Fact]
    public async Task A_picked_hero_draws_their_talents_in_row_then_column_order()
    {
        var hero = First;

        var page = await Draw(Asking(hero));

        Assert.Equal(
            [.. Tree(hero).SelectMany(row => row.Nodes).Select(node => Ui().Name(node.Talent))], Named(page));
    }

    [Fact]
    public void Every_hero_lays_out_a_complete_tree()
    {
        var slots = Store.Entities.OfType<TalentSlotDocument>().Count();
        var laid = 0;

        foreach (var hero in Enum.GetValues<Hero>())
        {
            var tree = Tree(hero);

            Assert.NotEmpty(tree);
            Assert.All(tree, row => Assert.NotEmpty(row.Nodes));

            laid += tree.Sum(row => row.Nodes.Count);
        }

        Assert.Equal(slots, laid);
    }

    [Fact]
    public async Task Every_cell_is_a_link_that_toggles_its_own_id_in_the_picks()
    {
        var hero = First;
        var node = Tree(hero)[0].Nodes[0];

        var bare = await Draw(Asking(hero));

        Assert.Contains($"href=\"{Page}?hero={hero}&amp;picks={node.Id}\"", bare, StringComparison.Ordinal);

        var picked = await Draw($"{Asking(hero)}&picks={node.Id}");

        Assert.Contains($"href=\"{Page}?hero={hero}\" aria-current=\"true\"", picked, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_picked_talent_reads_as_current_and_spends_its_cost()
    {
        var hero = First;
        var node = Tree(hero)[0].Nodes[0];

        var page = await Draw($"{Asking(hero)}&picks={node.Id}");

        Assert.Single(Cell(page), cell => cell.Contains("aria-current=\"true\"", StringComparison.Ordinal));
        Assert.Contains(
            $">{node.Cost}</b> of {TalentPlan.Points} points spent", page.Bare(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_id_no_slot_in_the_tree_carries_is_dropped_from_the_picks()
    {
        var hero = First;

        var page = await Draw($"{Asking(hero)}&picks=0,-4,999999");

        Assert.DoesNotContain("aria-current=\"true\"", page, StringComparison.Ordinal);
        Assert.Contains($">0</b> of {TalentPlan.Points} points spent", page.Bare(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Spending_the_whole_budget_leaves_every_talent_still_unpicked_unclickable()
    {
        var hero = First;
        var tree = Tree(hero);

        var taken = new HashSet<int>();
        var spent = 0;

        foreach (var node in tree.SelectMany(row => row.Nodes))
        {
            if (spent + node.Cost > TalentPlan.Points) continue;

            taken.Add(node.Id);
            spent += node.Cost;
        }

        Assert.Equal(TalentPlan.Points, spent);

        var page = await Draw($"{Asking(hero)}&picks={TalentPlan.Write(tree, taken)}");
        var cells = Cell(page);

        Assert.Equal(
            taken.Count, cells.Count(cell => cell.Contains("aria-current=\"true\"", StringComparison.Ordinal)));

        Assert.Equal(
            cells.Length - taken.Count,
            cells.Count(cell =>
                cell.StartsWith("<span", StringComparison.Ordinal) &&
                cell.Contains("aria-disabled=\"true\"", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_talent_beyond_the_points_left_says_why_it_cannot_be_taken()
    {
        var hero = First;
        var tree = Tree(hero);
        var dearest = tree.SelectMany(row => row.Nodes).MaxBy(node => node.Cost)!;

        var affordable = new HashSet<int>();
        var spent = 0;

        foreach (var node in tree.SelectMany(row => row.Nodes))
        {
            if (node.Id == dearest.Id || spent + node.Cost > TalentPlan.Points - dearest.Cost + 1) continue;

            affordable.Add(node.Id);
            spent += node.Cost;
        }

        var page = await Draw($"{Asking(hero)}&picks={TalentPlan.Write(tree, affordable)}");

        Assert.Contains(
            $"costs {dearest.Cost}, and only {TalentPlan.Points - spent} of the {TalentPlan.Points} points are left.",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_hero_draws_the_picker_alone()
    {
        var page = await Draw("?hero=Nobody");

        Assert.Empty(Cell(page));
        Assert.Contains("No hero picked.", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_talent_row_in_the_build_has_a_numbered_plate()
    {
        var plates = Store.Settings.TalentTierPlates;

        Assert.NotEmpty(plates);

        foreach (var hero in Enum.GetValues<Hero>())
        {
            Assert.All(
                Tree(hero),
                row => Assert.True(plates.ContainsKey(row.Number), $"{hero} row {row.Number} has no plate"));
        }
    }

    [Fact]
    public async Task Every_row_draws_the_plate_the_build_ships_for_its_number()
    {
        var hero = First;
        var page = await Draw(Asking(hero));

        foreach (var row in Tree(hero))
        {
            Assert.Contains(
                $"--cx-tal-plate:url({Chrome().TalentTier(row.Number)})", page, StringComparison.Ordinal);
        }

        Assert.Contains($"--cx-tal-band:url({Chrome().TalentBand})", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_untouched_tree_wears_the_unpicked_button_texture_on_every_cell()
    {
        var page = await Draw(Asking(First));
        var unpicked = Slot("disabled");

        Assert.All(Whole(page), cell => Assert.Contains(unpicked, cell, StringComparison.Ordinal));
        Assert.DoesNotContain(Slot("selected"), page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_cell_ever_wears_the_green_available_button_texture()
    {
        var hero = First;
        var node = Tree(hero)[0].Nodes[0];

        foreach (var query in new[] { Asking(hero), $"{Asking(hero)}&picks={node.Id}" })
        {
            Assert.DoesNotContain(Slot("default"), await Draw(query), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Only_a_talent_beyond_the_points_left_is_faded()
    {
        var hero = First;
        var tree = Tree(hero);
        var node = tree[0].Nodes[0];

        var early = await Draw($"{Asking(hero)}&picks={node.Id}");

        Assert.DoesNotContain("aria-disabled=\"true\" tabindex", early, StringComparison.Ordinal);

        var taken = new HashSet<int>();
        var spent = 0;

        foreach (var talent in tree.SelectMany(row => row.Nodes))
        {
            if (spent + talent.Cost > TalentPlan.Points) continue;

            taken.Add(talent.Id);
            spent += talent.Cost;
        }

        var full = await Draw($"{Asking(hero)}&picks={TalentPlan.Write(tree, taken)}");

        Assert.Equal(
            Whole(full).Length - taken.Count,
            Whole(full).Count(cell => cell.Contains("aria-disabled=\"true\"", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_picked_cell_wears_the_selected_button_texture_and_the_rest_do_not()
    {
        var hero = First;
        var node = Tree(hero)[0].Nodes[0];

        var page = await Draw($"{Asking(hero)}&picks={node.Id}");
        var mine = Whole(page).Single(cell =>
            cell.Contains($"/api/talent/{node.Id}/tooltip", StringComparison.Ordinal));

        Assert.Contains(Slot("selected"), mine, StringComparison.Ordinal);
        Assert.Single(Whole(page), cell => cell.Contains(Slot("selected"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_talent_beyond_the_points_left_wears_the_disabled_button_texture()
    {
        var hero = First;
        var tree = Tree(hero);

        var taken = new HashSet<int>();
        var spent = 0;

        foreach (var node in tree.SelectMany(row => row.Nodes))
        {
            if (spent + node.Cost > TalentPlan.Points) continue;

            taken.Add(node.Id);
            spent += node.Cost;
        }

        var page = await Draw($"{Asking(hero)}&picks={TalentPlan.Write(tree, taken)}");

        Assert.Equal(
            taken.Count, Whole(page).Count(cell => cell.Contains(Slot("selected"), StringComparison.Ordinal)));

        Assert.Equal(
            Whole(page).Length - taken.Count,
            Whole(page).Count(cell => cell.Contains(Slot("disabled"), StringComparison.Ordinal)));
    }

    private string Slot(string state) => $"--cx-tal-slot:url({Chrome().TalentSlot(state)})";

    [Fact]
    public async Task Every_cell_carries_the_tooltip_address_of_the_talent_it_draws()
    {
        var hero = First;
        var page = await Draw(Asking(hero));

        foreach (var node in Tree(hero).SelectMany(row => row.Nodes))
        {
            Assert.Contains($"data-tip=\"{Ui().Tip(node.Talent).Address}\"", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_picked_talent_wears_the_ornate_frame_the_node_widget_overlays()
    {
        var hero = First;
        var node = Tree(hero)[0].Nodes[0];

        var page = await Draw($"{Asking(hero)}&picks={node.Id}");

        Assert.Contains($"--cx-tal-frame:url({Chrome().TalentFrame})", page, StringComparison.Ordinal);
        Assert.EndsWith(
            "T_Borders_Frame_Pattern-core-primary-200.png", Chrome().TalentFrame, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_cell_asks_its_tooltip_for_art_the_cell_already_draws()
    {
        var page = await Draw(Asking(First));

        Assert.DoesNotContain(nameof(TalentTooltipRequest.ShowIcon), page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_tooltip_anchors_to_the_icon_and_name_rather_than_the_whole_cell()
    {
        var page = (await Draw(Asking(First))).Bare();

        Assert.Equal(
            Cell(page).Length,
            Regex.Count(page, "<span class=\"cx-tal-face\" data-tip=\""));

        Assert.All(
            Cells().Matches(page).Select(cell => cell.Value),
            cell => Assert.DoesNotContain("data-tip", cell, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_pick_travels_by_href_so_the_grid_needs_no_script()
    {
        var page = await Draw($"{Asking(First)}&picks={Tree(First)[0].Nodes[0].Id}");
        var cells = Cell(page);

        Assert.NotEmpty(cells);
        Assert.All(cells, cell => Assert.True(
            cell.StartsWith("<a class=\"cx-tal\" href=\"", StringComparison.Ordinal) ||
            cell.Contains("aria-disabled=\"true\"", StringComparison.Ordinal),
            cell));
    }

    [Fact]
    public async Task The_masthead_carries_a_tools_menu_that_opens_the_calculator()
    {
        var page = await Draw();

        Assert.Contains(">Tools</summary>", page.Bare(), StringComparison.Ordinal);
        Assert.Contains($"href=\"{Page}\"", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(6, "VI")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    public void A_row_number_prints_as_its_roman_numeral(int number, string numeral) =>
        Assert.Equal(numeral, TalentPlan.Numeral(number));

    private ICodexViewService Ui() => factory.Services.GetRequiredService<ICodexViewService>();

    private IChromeAssetService Chrome() => factory.Services.GetRequiredService<IChromeAssetService>();
}
