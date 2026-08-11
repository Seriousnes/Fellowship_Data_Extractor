using System.Text.Json;

using AngleSharp;

using Fellowship.SDK;
using Fellowship.SDK.Models;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Tests;

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
    public void Noor_at_Eternal_44_reads_the_health_the_game_shows()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var health = Difficulties.Health(Noor, Settings, difficulty);

        Assert.NotNull(health);
        Assert.Equal(NoorAtEternal44, health.Value, NoorAtEternal44 * 0.0001);
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
    public async Task A_creature_page_offers_every_difficulty_the_ladder_carries()
    {
        var page = await Page($"/creature/{NoorTheBetrayer}");

        var picker = page.QuerySelector("select[data-difficulty]");

        Assert.NotNull(picker);

        var offered = picker.QuerySelectorAll("option").ToArray();

        Assert.Equal(Settings.DifficultyScaling.Health.Count, offered.Length);

        var deep = offered.Single(
            option => option.GetAttribute("value") == (Eternal.MinimumDifficulty + 44).ToString());

        Assert.Contains($"{Eternal.Name} +44", deep.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_creature_page_reads_its_health_at_the_difficulty_it_is_asked_for()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var page = await Page($"/creature/{NoorTheBetrayer}?difficulty={difficulty}");

        var panel = page.QuerySelectorAll("div.cx-panel")
            .Single(element => element.QuerySelector(":scope > div.cx-panel-head")
                ?.TextContent.StartsWith($"At {Eternal.Name} +44", StringComparison.Ordinal) is true);

        var facts = panel.QuerySelectorAll("table.cx-facts tr")
            .ToDictionary(
                row => row.QuerySelector("th")!.TextContent,
                row => row.QuerySelector("td")!.TextContent.Trim(),
                StringComparer.Ordinal);

        var health = Difficulties.Health(Noor, Settings, difficulty);

        Assert.NotNull(health);
        Assert.Equal(Codex.Magnitude((decimal)health.Value), facts["Health"]);
        Assert.Equal(difficulty.ToString(), facts["Difficulty"]);
    }

    [Fact]
    public async Task A_creature_page_states_what_each_ability_hits_for_at_that_difficulty()
    {
        var difficulty = Eternal.MinimumDifficulty + 44;

        var page = await Page($"/creature/{NoorTheBetrayer}?difficulty={difficulty}");

        var panel = page.QuerySelectorAll("div.cx-panel")
            .Single(element => element.QuerySelector(":scope > div.cx-panel-head")
                ?.TextContent.StartsWith("Abilities", StringComparison.Ordinal) is true);

        var heads = panel.QuerySelectorAll("thead th").Select(cell => cell.TextContent).ToArray();

        Assert.Contains($"Damage at {Eternal.Name} +44", heads);

        var arrow = RouteTests.Factory.Store.Entities
            .OfType<AbilityDocument>()
            .Single(row => row.Id == BetrayersArrow);

        var band = Difficulties.Damage(arrow.Coefficients![0], Noor, Settings, difficulty);

        Assert.NotNull(band);
        Assert.Contains(Codex.Magnitude(band.Low), panel.TextContent, StringComparison.Ordinal);
    }
}
