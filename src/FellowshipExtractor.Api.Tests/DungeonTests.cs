using System.Text.Json;

namespace FellowshipExtractor.Api.Tests;

public sealed class DungeonTests : IClassFixture<RouteTests.Factory>
{
    private readonly HttpClient client;

    public DungeonTests(RouteTests.Factory factory) => client = factory.CreateClient();

    private static DungeonDocument Any =>
        RouteTests.Factory.Store.Entities.OfType<DungeonDocument>().First(row => row.Name is not null);

    private static DungeonDocument Populated =>
        RouteTests.Factory.Store.Entities
            .OfType<DungeonDocument>()
            .First(dungeon => RouteTests.Factory.Store.Entities
                .OfType<CreatureDocument>()
                .Any(creature => creature.FoundIn?.Any(level => level.Id == dungeon.Id) is true));

    [Fact]
    public async Task A_dungeon_answers_its_record_at_its_own_address()
    {
        var dungeon = Any;

        var body = await client.GetStringAsync(
            $"/api/dungeon/{dungeon.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        Assert.Equal("dungeon", reading.RootElement.GetProperty("type").GetString());
        Assert.Equal(dungeon.Name, reading.RootElement.GetProperty("name").GetString());
        Assert.Equal(dungeon.MaxPlayers, reading.RootElement.GetProperty("maxPlayers").GetInt32());
        Assert.Equal(dungeon.QualifierTime, reading.RootElement.GetProperty("qualifierTime").GetDouble());
        Assert.Equal(dungeon.RequiredScore, reading.RootElement.GetProperty("requiredScore").GetInt32());
    }

    [Fact]
    public async Task A_dungeon_record_lists_every_creature_found_there()
    {
        var dungeon = Populated;

        var body = await client.GetStringAsync(
            $"/api/dungeon/{dungeon.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        var creatures = reading.RootElement.GetProperty("creatures").EnumerateArray().ToArray();

        Assert.NotEmpty(creatures);
        Assert.All(creatures, creature => Assert.Equal("creature", creature.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task A_dungeon_answers_its_tooltip_at_its_own_address()
    {
        var response = await client.GetAsync(
            $"/api/dungeon/{Any.Id}/tooltip", Xunit.TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_creature_record_names_the_dungeons_it_is_found_in()
    {
        var creature = RouteTests.Factory.Store.Entities
            .OfType<CreatureDocument>()
            .First(row => row.FoundIn is { Count: > 0 });

        var body = await client.GetStringAsync(
            $"/api/creature/{creature.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        var found = reading.RootElement.GetProperty("foundIn").EnumerateArray().ToArray();

        Assert.NotEmpty(found);
        Assert.All(found, level => Assert.Equal("dungeon", level.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task A_creature_record_states_its_rank_and_what_it_is_worth_to_kill()
    {
        var creature = RouteTests.Factory.Store.Entities
            .OfType<CreatureDocument>()
            .First(row => row is { Rank: not null, KillScore: not null, Health: not null });

        var body = await client.GetStringAsync(
            $"/api/creature/{creature.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        Assert.Equal(creature.Rank!.Value.ToString(), reading.RootElement.GetProperty("rank").GetString());
        Assert.Equal(creature.KillScore, reading.RootElement.GetProperty("killScore").GetInt32());
        Assert.Equal(creature.Health, reading.RootElement.GetProperty("health").GetDouble());
    }

    [Fact]
    public async Task A_creature_record_states_each_ability_with_its_phase_and_whether_the_game_shows_it()
    {
        var creature = RouteTests.Factory.Store.Entities
            .OfType<CreatureDocument>()
            .First(row => row.Abilities is { Count: > 0 });

        var body = await client.GetStringAsync(
            $"/api/creature/{creature.Id}", Xunit.TestContext.Current.CancellationToken);

        using var reading = JsonDocument.Parse(body);

        var abilities = reading.RootElement.GetProperty("abilities").EnumerateArray().ToArray();

        Assert.NotEmpty(abilities);
        Assert.All(abilities, slot =>
        {
            Assert.False(string.IsNullOrWhiteSpace(slot.GetProperty("phase").GetString()));
            Assert.True(slot.TryGetProperty("shown", out _));
            Assert.False(string.IsNullOrWhiteSpace(slot.GetProperty("ability").GetProperty("resource").GetString()));
        });
    }
}
