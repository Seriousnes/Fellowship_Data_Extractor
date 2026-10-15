namespace FellowshipExtractor.Tests;

public sealed class CreatureStatTests
{
    [Fact]
    public void EveryCreatureTheTableStatesAnythingAboutStatesItsHealth()
    {
        var creatures = Export.Loaded.Documents.Where(row => row.Type == EntityType.Creature).ToArray();

        Assert.NotEmpty(creatures);

        var attributes = new[] { "killScore", "spiritPoints", "armor", "resist" };

        var stating = creatures
            .Where(row => attributes.Any(name => row.Body[name] is not null))
            .ToArray();

        Assert.NotEmpty(stating);
        Assert.All(stating, row => Assert.NotNull(row.Body["health"]));

        var silent = creatures.Where(row => row.Body["health"] is null).ToArray();

        Assert.All(silent, row => Assert.All(attributes, name => Assert.Null(row.Body[name])));
    }
}
