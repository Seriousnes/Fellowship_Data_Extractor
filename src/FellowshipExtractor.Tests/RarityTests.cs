using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class RarityTests
{
    private const string LegendaryRuleSet = "Legendary";

    private static IEnumerable<(int Id, string? Name, JsonObject Body)> Items() =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item)
            .Select(document => (document.Id, document.Name, document.Body));

    private static IReadOnlyList<string> Rungs(JsonObject body) =>
        body["rarities"] is JsonArray rungs ? [.. rungs.Select(rung => rung!.GetValue<string>())] : [];

    private static int Tier(string rung) =>
        Export.Loaded.Settings.Rarities.Single(entry => entry.Name == rung).Tier;

    [Fact]
    public void EveryItemThatDropsStatesTheRungsItDropsIn()
    {
        var missing = Items()
            .Where(item => item.Body["ruleSet"] is not null && Rungs(item.Body).Count == 0)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{missing.Length} items carry a rule set but no rarities, so a tooltip has no rung to answer at. " +
            $"First: {string.Join(", ", missing.Take(5).Select(item => $"{item.Id} {item.Name}"))}");
    }

    [Fact]
    public void ADroppedItemToppsOutAtTheRungThePrintsAsRegal()
    {
        var regal = Export.Loaded.Settings.Rarities.Single(rung => rung.DisplayName == "Regal");

        var dropped = Items()
            .Where(item => item.Body["ruleSet"]?.GetValue<string>() is { } set && set != LegendaryRuleSet && Rungs(item.Body).Count > 1)
            .ToArray();

        Assert.NotEmpty(dropped);

        Assert.All(dropped, item => Assert.Equal(regal.Tier, Rungs(item.Body).Max(Tier)));
    }

    [Fact]
    public void ALegendaryItemComesOnlyInLegendary()
    {
        var legendaries = Items()
            .Where(item => item.Body["ruleSet"]?.GetValue<string>() == LegendaryRuleSet)
            .ToArray();

        Assert.NotEmpty(legendaries);

        Assert.All(legendaries, item => Assert.Equal([LegendaryRuleSet], Rungs(item.Body)));
    }

    [Fact]
    public void AnItemFixedAtARungComesInThatRungAlone()
    {
        var fixedRung = Items()
            .Where(item => item.Body["rarity"] is not null && item.Body["ruleSet"] is not null)
            .ToArray();

        Assert.NotEmpty(fixedRung);

        Assert.All(fixedRung, item =>
            Assert.Equal([item.Body["rarity"]!.GetValue<string>()], Rungs(item.Body)));
    }

    [Fact]
    public void AnItemWhoseOwnRowStatesItsRungDropsInNoRungAtAll()
    {
        var stated = Items()
            .Where(item => item.Body["rarity"] is not null && item.Body["ruleSet"] is null)
            .ToArray();

        Assert.Equal(226, stated.Length);

        Assert.All(stated, item => Assert.Empty(Rungs(item.Body)));
        Assert.All(stated, item => Assert.Null(item.Body["stats"]));
    }

    [Fact]
    public void AFixedHeroItemComesInTheRungItsRowStates()
    {
        var floor = Export.Loaded.Settings.Rarities.MinBy(rung => rung.Tier)!;

        var heroItems = Items()
            .Where(item => item.Body["ruleSet"] is null && item.Body["stats"] is JsonArray)
            .ToArray();

        Assert.NotEmpty(heroItems);

        Assert.All(heroItems, item => Assert.Equal([floor.Name], Rungs(item.Body)));
    }

    private static IEnumerable<(int Id, string? Name, JsonObject Body)> Currencies() =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Currency)
            .Select(document => (document.Id, document.Name, document.Body));

    [Fact]
    public void EveryCurrencyTheTableNamesStatesTheRungItIs()
    {
        var named = Currencies().Where(currency => currency.Name is not null).ToArray();

        Assert.NotEmpty(named);

        Assert.All(named, currency => Assert.NotNull(currency.Body["rarity"]));
    }

    [Fact]
    public void EveryCurrencyRungIsARungOfTheLadder()
    {
        var rungs = Currencies()
            .Select(currency => currency.Body["rarity"]?.GetValue<string>())
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(rungs);
        Assert.All(rungs, rung => Assert.Contains(rung, Export.Loaded.Settings.Rarities.Select(entry => entry.Name)));
    }

    private static DropItemLevel Level(string row) =>
        Export.Loaded.Settings.DropItemLevels.Single(level => level.Row == row);

    [Fact]
    public void TheDropLadderTopsAt315AndALegendaryDropAt360()
    {
        Assert.Equal(
            new DropItemLevel(FellowshipExtractor.Extraction.DropItemLevels.DefaultRow, 21, 315),
            Level(FellowshipExtractor.Extraction.DropItemLevels.DefaultRow));

        Assert.Equal(new DropItemLevel("Legendary", 21, 360), Level("Legendary"));
    }

    [Fact]
    public void EveryDropItemLevelIsAMultipleOfFifteen()
    {
        Assert.NotEmpty(Export.Loaded.Settings.DropItemLevels);

        Assert.All(Export.Loaded.Settings.DropItemLevels, level => Assert.Equal(0, level.ItemLevel % 15));
    }

    [Fact]
    public void NoRowWithoutARarityLadderStatesRungs()
    {
        var stated = Export.Loaded.Documents
            .Where(document => document.Type != EntityType.Item && document.Body["rarities"] is not null)
            .ToArray();

        Assert.True(
            stated.Length == 0,
            $"{stated.Length} rows outside items state rarities. First: " +
            string.Join(", ", stated.Take(5).Select(document => $"{document.Type} {document.Id}")));
    }
}
