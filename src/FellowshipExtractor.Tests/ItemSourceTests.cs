using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class ItemSourceTests
{
    private const int SecondThresholdOnly = 4;

    [Fact]
    public void EveryItemThatCarriesStatsStatesWhereItComesFrom()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        Assert.Equal(1956, carrying.Length);

        var unsourced = carrying.Where(document => document.Body["sources"] is not JsonArray).ToArray();

        Assert.Equal(1952, carrying.Length - unsourced.Length);
        Assert.Equal(SecondThresholdOnly, unsourced.Length);
    }

    [Fact]
    public void EveryItemThatCarriesStatsArrivesAtAStatedItemLevel()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        var unlevelled = carrying.Where(document => document.Body["itemLevel"] is null).ToArray();

        Assert.Equal(1952, carrying.Length - unlevelled.Length);
        Assert.Equal(SecondThresholdOnly, unlevelled.Length);
    }
}
