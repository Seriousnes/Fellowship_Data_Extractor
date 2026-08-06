namespace FellowshipExtractor.Tests;

public sealed class StatPoolTests
{
    [Fact]
    public void PrimaryPoolCoefficientsAreUnchanged()
    {
        var pool = Export.Loaded.Vocabulary.StatCalculation.PrimaryPool;

        Assert.NotNull(pool);
        Assert.Equal(15, pool!.MinimumItemLevel);
        Assert.Equal(15, pool.LevelDivisor);
        Assert.Equal(1.08, pool.GrowthBase);
        Assert.Equal(6, pool.Multiplier);
    }

    [Fact]
    public void SecondaryPoolCoefficientsAreUnchanged()
    {
        var pool = Export.Loaded.Vocabulary.StatCalculation.SecondaryPool;

        Assert.NotNull(pool);
        Assert.Equal(15, pool!.MinimumItemLevel);
        Assert.Equal(72, pool.LevelMultiplier);
        Assert.Equal(2.4, pool.Multiplier);
    }

    [Fact]
    public void RarityStillDoesNotScaleMagnitudes() =>
        Assert.Equal(1, Export.Loaded.Vocabulary.StatCalculation.StatRarityBase);

    [Fact]
    public void PoolsRiseWithItemLevel()
    {
        var primary = Export.Loaded.Vocabulary.StatCalculation.PrimaryPool;
        var secondary = Export.Loaded.Vocabulary.StatCalculation.SecondaryPool;

        Assert.NotNull(primary);
        Assert.NotNull(secondary);

        Assert.True(primary!.GrowthBase > 1, $"Growth base is {primary.GrowthBase}, so the pool shrinks with level.");
        Assert.True(primary.LevelDivisor > 0, "A level divisor of zero or less cannot be raised to.");
        Assert.True(primary.Multiplier > 0, "A pool multiplier at or below zero gives every item nothing.");
        Assert.True(secondary!.LevelMultiplier > 0, "A secondary level multiplier at or below zero has no root.");
        Assert.True(secondary.Multiplier > 0, "A secondary multiplier at or below zero gives every item nothing.");
    }
}
