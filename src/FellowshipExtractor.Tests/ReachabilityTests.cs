using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class ReachabilityTests
{
    private static IReadOnlyList<(EntityType Type, int Id, JsonObject Body)> Rows =>
        [.. Export.Loaded.Documents.Select(row => (row.Type, row.Id, row.Body))];

    private static bool Enabled(JsonObject body) => body["enabledInGame"]?.GetValue<bool>() is not false;

    private static IReadOnlyDictionary<int, bool> Dungeons() =>
        Rows.Where(row => row.Type == EntityType.Dungeon).ToDictionary(row => row.Id, row => Enabled(row.Body));

    private static IEnumerable<int> Ids(JsonNode? references) =>
        references is JsonArray array
            ? array.Select(reference => reference!["id"]!.GetValue<int>())
            : [];

    [Theory]
    [InlineData("Journey To Stronghold")]
    [InlineData("Wyrmheart")]
    [InlineData("Sailor's Abyss")]
    public void A_map_the_game_disables_or_rotates_out_is_disabled(string name) =>
        Assert.False(Enabled(Rows.Single(row => row.Type == EntityType.Dungeon &&
                                                 row.Body["name"]?.GetValue<string>() == name).Body));

    [Theory]
    [InlineData("Stormwatch")]
    [InlineData("Cithrel's Fall")]
    [InlineData("The Stronghold")]
    public void A_map_in_rotation_is_enabled(string name) =>
        Assert.True(Enabled(Rows.Single(row => row.Type == EntityType.Dungeon &&
                                                row.Body["name"]?.GetValue<string>() == name).Body));

    [Fact]
    public void A_creature_is_disabled_exactly_when_every_map_it_is_found_in_is_disabled()
    {
        var dungeons = Dungeons();

        var placed = Rows
            .Where(row => row.Type == EntityType.Creature && row.Body["foundIn"] is JsonArray { Count: > 0 })
            .ToArray();

        Assert.Contains(placed, row => !Enabled(row.Body));
        Assert.All(placed, row => Assert.Equal(
            Ids(row.Body["foundIn"]).Any(id => dungeons.GetValueOrDefault(id, true)),
            Enabled(row.Body)));
    }

    [Fact]
    public void An_ability_only_disabled_creatures_reach_is_disabled()
    {
        var creatures = Rows
            .Where(row => row.Type == EntityType.Creature)
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.Any(row => Enabled(row.Body)));

        var listedByEnabled = Rows
            .Where(row => row.Type == EntityType.Creature && Enabled(row.Body))
            .SelectMany(row => new[] { row.Body["abilities"], row.Body["passives"] })
            .OfType<JsonArray>()
            .SelectMany(list => list)
            .Where(entry => entry?["ability"]?["type"]?.GetValue<string>() == "ability")
            .Select(entry => entry!["ability"]!["id"]!.GetValue<int>())
            .ToHashSet();

        var owned = Rows
            .Where(row => row.Type == EntityType.Ability &&
                          row.Body["owner"]?["type"]?.GetValue<string>() == "creature" &&
                          !creatures.GetValueOrDefault(row.Body["owner"]!["id"]!.GetValue<int>(), true) &&
                          !listedByEnabled.Contains(row.Id))
            .ToArray();

        Assert.NotEmpty(owned);
        Assert.All(owned, row => Assert.False(Enabled(row.Body)));
    }
}
