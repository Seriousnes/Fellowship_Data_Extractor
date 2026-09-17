namespace FellowshipExtractor.Tests;

public sealed class DifficultyLadderTests
{
    [Fact]
    public void EveryCreatureTheTableStatesAnythingAboutStatesEveryPrimaryAttribute()
    {
        var creatures = Export.Loaded.Documents.Where(row => row.Type == EntityType.Creature).ToArray();

        Assert.NotEmpty(creatures);

        var attributes = new[] { "intellect", "strength", "agility" };

        var stating = creatures.Where(row => row.Body["health"] is not null).ToArray();

        Assert.NotEmpty(stating);
        Assert.All(stating, row => Assert.All(attributes, name => Assert.NotNull(row.Body[name])));

        var silent = creatures.Where(row => row.Body["health"] is null).ToArray();

        Assert.All(silent, row => Assert.All(attributes, name => Assert.Null(row.Body[name])));
    }
}
