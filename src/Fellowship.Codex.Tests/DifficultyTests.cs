using System.Text.Json;

using AngleSharp;

using Fellowship.SDK;
using Fellowship.SDK.Models;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Tests;

public sealed class DifficultyTests : IClassFixture<RouteTests.Factory>
{
    private const int NoorTheBetrayer = 168;

    private const int BetrayersArrow = 750;

    private const double NoorAtEternal44 = 12_365_031;

    private readonly HttpClient client;

    public DifficultyTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static SettingsFile Settings => RouteTests.Factory.Store.Settings;

    private static LeagueEntry Eternal =>
        Settings.Leagues.Single(league => league.Kind == LeagueKind.Eternal);

    private static CreatureDocument Noor =>
        RouteTests.Factory.Store.Entities.OfType<CreatureDocument>().Single(row => row.Id == NoorTheBetrayer);

    [Fact]
    public void Noor_at_Eternal_44_reads_within_a_hundredth_of_a_percent_of_the_figure_the_game_showed()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var health = Difficulties.Health(Noor, Settings, difficulty);

        Assert.NotNull(health);
        Assert.Equal(NoorAtEternal44, health.Value, NoorAtEternal44 * 0.0001);
        Assert.Equal(12_364_857d, health.Value, 1d);
    }

    [Fact]
    public async Task A_creature_tooltip_states_its_health_at_the_difficulty_it_is_asked_for()
    {
        var opening = await client.GetStringAsync(
            $"/api/creature/{NoorTheBetrayer}/tooltip", Xunit.TestContext.Current.CancellationToken);

        var deep = await client.GetStringAsync(
            $"/api/creature/{NoorTheBetrayer}/tooltip?difficulty={Eternal.MinimumDifficulty + 44}",
            Xunit.TestContext.Current.CancellationToken);

        var health = Difficulties.Health(Noor, Settings, Eternal.MinimumDifficulty + 44);

        Assert.NotNull(health);
        Assert.Contains("Health", opening, StringComparison.Ordinal);
        Assert.Contains(FellowshipCodex.Magnitude((decimal)Noor.Health!.Value), opening, StringComparison.Ordinal);
        Assert.Contains(FellowshipCodex.Magnitude((decimal)health.Value), deep, StringComparison.Ordinal);
    }

    [Fact]
    public void The_ladder_labels_every_difficulty_by_the_league_that_covers_it()
    {
        var ladder = Difficulties.Ladder(Settings);

        Assert.Equal(Settings.DifficultyScaling.Health.Count, ladder.Count);

        var eternal = Eternal;

        var opening = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty);

        Assert.Equal(eternal.Name, opening.League);
        Assert.Equal(0, opening.Step);
        Assert.Equal($"{eternal.Name} +0", opening.Label);

        var deep = ladder.Single(step => step.Difficulty == eternal.MinimumDifficulty + 44);

        Assert.Equal($"{eternal.Name} +44", deep.Label);
    }

    [Fact]
    public void A_difficulty_no_ladder_carries_falls_back_to_the_first_one()
    {
        var store = RouteTests.Factory.Store;

        var lowest = Settings.DifficultyScaling.Health.Keys.Min();

        Assert.Equal(lowest, store.Difficulty(null));
        Assert.Equal(lowest, store.Difficulty("nonsense"));
        Assert.Equal(lowest, store.Difficulty("100000"));
        Assert.Equal(65, store.Difficulty("65"));
    }

    [Fact]
    public void A_creature_ability_states_the_damage_its_coefficient_and_the_ladder_give()
    {
        var arrow = RouteTests.Factory.Store.Entities
            .OfType<AbilityDocument>()
            .Single(row => row.Id == BetrayersArrow);

        var coefficient = Assert.Single(arrow.Coefficients!);

        var difficulty = Eternal.MinimumDifficulty + 44;

        var band = Difficulties.Damage(coefficient, Noor, Settings, difficulty);

        Assert.NotNull(band);

        var scale = Difficulties.DamageScale(Settings, Noor.Scaling, difficulty);

        Assert.NotNull(scale);
        Assert.Equal(
            Noor.Intellect!.Value * coefficient.Value * scale.Value,
            (double)band.Mean,
            0.01);

        Assert.True(band.Low < band.Mean && band.Mean < band.High, "The band does not straddle its mean.");
    }

    [Fact]
    public async Task A_creature_record_carries_the_attributes_its_abilities_scale_off()
    {
        var body = await client.GetStringAsync(
            $"/api/creature/{NoorTheBetrayer}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        Assert.Equal(Noor.Intellect, reading.RootElement.GetProperty("intellect").GetDouble());
        Assert.Equal(Noor.Strength, reading.RootElement.GetProperty("strength").GetDouble());
        Assert.Equal(Noor.Agility, reading.RootElement.GetProperty("agility").GetDouble());
    }

    [Fact]
    public async Task An_ability_record_carries_the_coefficients_it_states()
    {
        var body = await client.GetStringAsync(
            $"/api/ability/{BetrayersArrow}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        var stated = reading.RootElement.GetProperty("coefficients").EnumerateArray().ToArray();

        var only = Assert.Single(stated);

        Assert.Equal("Intellect", only.GetProperty("attribute").GetString());
        Assert.Equal(4.84, only.GetProperty("value").GetDouble());
    }

    [Fact]
    public async Task An_ability_tooltip_states_its_damage_only_once_a_difficulty_is_asked_for()
    {
        var opening = await client.GetStringAsync(
            $"/api/ability/{BetrayersArrow}/tooltip", Xunit.TestContext.Current.CancellationToken);

        var deep = await client.GetStringAsync(
            $"/api/ability/{BetrayersArrow}/tooltip?difficulty={Eternal.MinimumDifficulty + 44}",
            Xunit.TestContext.Current.CancellationToken);

        Assert.Contains("Damage", opening, StringComparison.Ordinal);
        Assert.Contains("Damage", deep, StringComparison.Ordinal);
        Assert.NotEqual(opening, deep);
    }

    private async Task<IDocument> Page(string address) =>
        await BrowsingContext.New(Configuration.Default).OpenAsync(
            request => request.Content(
                client.GetStringAsync(address, Xunit.TestContext.Current.CancellationToken)
                    .GetAwaiter()
                    .GetResult()),
            Xunit.TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_creature_page_carries_a_picker_reading_the_difficulty_it_stands_at()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var page = await Page($"/creature/{NoorTheBetrayer}?difficulty={difficulty}");

        var picker = page.QuerySelector("button[data-difficulty]");

        Assert.NotNull(picker);
        Assert.Equal("combobox", picker.GetAttribute("role"));
        Assert.Equal("false", picker.GetAttribute("aria-expanded"));
        Assert.Contains($"{Eternal.Name} +44", picker.TextContent, StringComparison.Ordinal);
        Assert.Contains(difficulty.ToString(), picker.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_creature_page_reads_its_health_at_the_difficulty_it_is_asked_for()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var page = await Page($"/creature/{NoorTheBetrayer}?difficulty={difficulty}");

        var facts = Named(page, "Details");

        var health = Difficulties.Health(Noor, Settings, difficulty);
        var scale = Difficulties.HealthScale(Settings, Noor.Scaling, difficulty);

        Assert.NotNull(health);
        Assert.NotNull(scale);
        Assert.Equal(FellowshipCodex.Magnitude((decimal)health.Value), facts["Health"]);
        Assert.StartsWith(FellowshipCodex.Number(scale.Value), facts["Health scale"], StringComparison.Ordinal);
        Assert.EndsWith("x", facts["Health scale"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_creature_states_where_it_is_found_among_its_particulars()
    {
        var page = await Page($"/creature/{NoorTheBetrayer}");

        var facts = Named(page, "Details");

        Assert.Equal(
            string.Join(", ", Noor.FoundIn!.Select(level => RouteTests.Factory.Store.All(level.Type, level.Id)[0])
                .Select(PublicTypes.NameOf)),
            facts["Location"]);
    }

    [Fact]
    public async Task A_creature_leaves_the_attributes_its_damage_is_worked_out_from_off_its_particulars()
    {
        var page = await Page($"/creature/{NoorTheBetrayer}");

        var facts = Named(page, "Details");

        Assert.DoesNotContain("Armor", facts.Keys);
        Assert.DoesNotContain("Resist", facts.Keys);
        Assert.DoesNotContain("Intellect", facts.Keys);
        Assert.DoesNotContain("Strength", facts.Keys);
        Assert.DoesNotContain("Agility", facts.Keys);
        Assert.Contains("Rank", facts.Keys);
    }

    private static IElement Panel(IDocument page, string head) =>
        page.QuerySelectorAll("div.cx-panel")
            .Single(element => element.QuerySelector(":scope > div.cx-panel-head")
                ?.TextContent.StartsWith(head, StringComparison.Ordinal) is true);

    private static IReadOnlyDictionary<string, string> Named(IDocument page, string head) =>
        Panel(page, head).QuerySelectorAll("table.cx-facts tr")
            .ToDictionary(
                row => row.QuerySelector("th")!.TextContent,
                row => row.QuerySelector("td")!.TextContent.Trim(),
                StringComparer.Ordinal);

    private static string[] Struck(IElement panel, string head)
    {
        var heads = panel.QuerySelectorAll("thead th").Select(cell => cell.TextContent).ToArray();
        var at = Array.IndexOf(heads, head);

        Assert.InRange(at, 0, heads.Length - 1);

        return
        [
            .. panel.QuerySelectorAll("tbody tr")
                .Select(row => row.QuerySelectorAll("td").ElementAtOrDefault(at)?.TextContent.Trim())
                .OfType<string>(),
        ];
    }

    [Fact]
    public async Task A_creature_page_hits_harder_the_deeper_the_difficulty_it_is_read_at()
    {
        var opening = Panel(
            await Page($"/creature/{NoorTheBetrayer}?difficulty={Eternal.MinimumDifficulty}"), "Abilities");

        var deep = Panel(
            await Page($"/creature/{NoorTheBetrayer}?difficulty={Eternal.MinimumDifficulty + 44}"), "Abilities");

        var pairs = Struck(opening, "Damage")
            .Zip(Struck(deep, "Damage"))
            .Where(pair => pair.First.Length > 0)
            .ToArray();

        Assert.NotEmpty(pairs);
        Assert.All(pairs, pair => Assert.NotEqual(pair.First, pair.Second));
    }

    [Fact]
    public async Task A_creature_page_states_what_each_ability_hits_for_at_that_difficulty()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var page = await Page($"/creature/{NoorTheBetrayer}?difficulty={difficulty}");

        var panel = Panel(page, "Abilities");

        var heads = panel.QuerySelectorAll("thead th").Select(cell => cell.TextContent).ToArray();

        Assert.Equal(["", "Name", "Damage", "Range", "Cooldown", ""], heads);

        var arrow = RouteTests.Factory.Store.Entities
            .OfType<AbilityDocument>()
            .Single(row => row.Id == BetrayersArrow);

        var band = Difficulties.Damage(arrow.Coefficients![0], Noor, Settings, difficulty);

        Assert.NotNull(band);
        Assert.Contains(FellowshipCodex.Magnitude(band.Low), panel.TextContent, StringComparison.Ordinal);
    }
}
