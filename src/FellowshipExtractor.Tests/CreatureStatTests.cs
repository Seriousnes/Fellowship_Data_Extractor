using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class CreatureStatTests
{
    private static readonly CreatureStats Stats = Export.Resolver.CreatureStats;

    [Fact]
    public void AnExplicitBaseHealthRowIsTheHealth()
    {
        var boss = Stats.For("CM_BossAina");

        Assert.NotNull(boss);
        Assert.Equal(32000, boss.Health);
    }

    [Fact]
    public void AMultiplierRowMultipliesTheDefaultBaseHealth()
    {
        var reaver = Stats.For("VV_IcebiteReaver");

        Assert.NotNull(reaver);
        Assert.Equal(2999 * 4.291, reaver.Health!.Value, 3);
        Assert.Equal(5, reaver.KillScore);
        Assert.Equal(5, reaver.SpiritPoints);
        Assert.Equal(0, reaver.Armor);
        Assert.Equal(0, reaver.Resist);
    }

    [Fact]
    public void NoNamespaceCarriesBothHealthRowsSoTheBranchIsUnambiguous()
    {
        Assert.True(Stats.Namespaces > 200, $"Only {Stats.Namespaces} namespaces read back.");
        Assert.Equal(0, Stats.BothHealthRows);
    }

    [Fact]
    public void EveryCreatureTheTableStatesAnythingAboutStatesItsHealth()
    {
        var creatures = Export.Loaded.Documents.Where(row => row.Type == EntityType.Creature).ToArray();

        Assert.NotEmpty(creatures);
        Assert.Equal(0, Stats.Healthless);

        var attributes = new[] { "killScore", "spiritPoints", "armor", "resist" };

        var stating = creatures
            .Where(row => attributes.Any(name => row.Body[name] is not null))
            .ToArray();

        Assert.NotEmpty(stating);
        Assert.All(stating, row => Assert.NotNull(row.Body["health"]));

        var silent = creatures.Where(row => row.Body["health"] is null).ToArray();

        Assert.All(silent, row => Assert.All(attributes, name => Assert.Null(row.Body[name])));
    }

    [Fact]
    public void TheDifficultyLaddersRunFromOneToOneHundredAndFiftyOne()
    {
        var scaling = Stats.Scaling;

        Assert.Equal(151, scaling.Health.Count);
        Assert.Equal(151, scaling.Damage.Count);
        Assert.Equal(1, scaling.Health[1]);
        Assert.Equal(1, scaling.Damage[1]);
        Assert.True(scaling.Health[151] > 1000, $"Health at 151 read {scaling.Health[151]}.");
        Assert.True(scaling.Damage[151] > 700, $"Damage at 151 read {scaling.Damage[151]}.");
    }
}
