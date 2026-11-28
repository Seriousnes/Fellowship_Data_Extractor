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

    /// <summary>
    /// Feral Spiritclaw has no <c>Haste</c> row and no character has an <c>AttackSpeed</c> row, so both are the
    /// <c>CRBasicAttributeSet</c> constructor's 1. <c>docs/casting-and-channeling.md</c> has the time rate reading them.
    /// </summary>
    [Fact]
    public void ACreatureWithNoHasteRowHasTheConstructorsHasteAndAttackSpeed()
    {
        const int feralSpiritclaw = 347;

        var creature = Export.Loaded.Documents.Single(row => row.Type == EntityType.Creature && row.Id == feralSpiritclaw);

        Assert.Equal(FellowshipExtractor.Extraction.CreatureStats.ConstructorHaste, (double)creature.Body["haste"]!);
        Assert.Equal(FellowshipExtractor.Extraction.CreatureStats.ConstructorAttackSpeed, (double)creature.Body["attackSpeed"]!);
    }

    [Fact]
    public void EveryCreatureWithAttributesHasABaseHasteAndAttackSpeed()
    {
        var stated = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Creature && row.Body["strength"] is not null)
            .ToArray();

        Assert.NotEmpty(stated);
        Assert.All(stated, row =>
        {
            Assert.True((double)row.Body["haste"]! > 0);
            Assert.True((double)row.Body["attackSpeed"]! > 0);
        });
    }
}
