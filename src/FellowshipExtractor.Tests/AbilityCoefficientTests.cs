using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class AbilityCoefficientTests
{
    private static readonly (EntityType Type, int Id, string? Name, JsonObject Body)[] Abilities =
        [.. Export.Loaded.Documents.Where(row => row.Type == EntityType.Ability)];

    private static JsonArray? Stated(JsonObject body) => body["coefficients"] as JsonArray;

    private static IEnumerable<JsonObject> Every() =>
        Abilities.Select(row => Stated(row.Body)).OfType<JsonArray>().SelectMany(list => list.OfType<JsonObject>());

    [Fact]
    public void ACreatureAbilityStatesTheCoefficientItsTuningRowCarries()
    {
        var arrow = Abilities.Single(row => row.Id == 750);

        var stated = Assert.IsType<JsonArray>(Stated(arrow.Body));
        var only = Assert.IsType<JsonObject>(Assert.Single(stated));

        Assert.Equal("Intellect", (string?)only["attribute"]);
        Assert.Equal(4.84, (double?)only["value"]);
        Assert.Equal(0.2, (double?)only["spread"]);
    }

    [Fact]
    public void EveryCoefficientNamesAnAttributeAndAMagnitudeAbovNothing()
    {
        var stated = Every().ToArray();

        Assert.NotEmpty(stated);

        Assert.All(stated, coefficient =>
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)coefficient["attribute"]));
            Assert.NotNull((double?)coefficient["value"]);
        });
    }

    [Fact]
    public void AnAbilityStatingTwoCoefficientsNamesThePartEachBelongsTo()
    {
        var several = Abilities
            .Where(row => Stated(row.Body) is { Count: > 1 })
            .ToArray();

        Assert.NotEmpty(several);

        Assert.All(several, row =>
        {
            var parts = Stated(row.Body)!
                .OfType<JsonObject>()
                .Select(coefficient => ((string?)coefficient["part"], (string?)coefficient["attribute"]))
                .ToArray();

            Assert.Equal(parts.Length, parts.Distinct().Count());
        });
    }

    [Fact]
    public void AStatedSpreadWinsOverTheBandThePresetDefaultsTo()
    {
        var toss = Abilities.Single(row => row.Id == 46);

        var stated = Assert.IsType<JsonArray>(Stated(toss.Body));
        var only = Assert.IsType<JsonObject>(Assert.Single(stated));

        Assert.Equal("Strength", (string?)only["attribute"]);
        Assert.Equal(0.4, (double?)only["spread"]);
    }

    [Fact]
    public void MostOfWhatACreatureWieldsStatesWhatItHits()
    {
        var owned = Abilities
            .Where(row => row.Body["owner"]?["type"]?.GetValue<string>() == "creature")
            .ToArray();

        Assert.NotEmpty(owned);

        var stating = owned.Count(row => Stated(row.Body) is { Count: > 0 });

        Assert.True(
            stating * 2 > owned.Length,
            $"Only {stating} of {owned.Length} creature abilities state a coefficient.");
    }
}
