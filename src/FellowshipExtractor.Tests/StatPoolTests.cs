namespace FellowshipExtractor.Tests;

/// <summary>
/// The item-level stat pools, which are not read from a table at all: StatCalculation decompiles
/// HeroItemStatCalculation's Blueprint bytecode, asserts the sequence of math calls it makes, and
/// takes the literals out of fixed positions in that sequence.
///
/// That makes them the most fragile numbers in the export and the ones most worth pinning. A
/// designer reordering the graph does not change the constants, it changes which constant sits at
/// each position, so the extractor would keep reading numbers and they would be the wrong ones. No
/// exception, no empty result, just different stats on every item.
/// </summary>
public sealed class StatPoolTests
{
    /// <summary>
    /// The primary pool: multiplier x growth^(level / divisor) x sqrt(level), above a floor.
    /// </summary>
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

    /// <summary>The secondary pool: multiplier x sqrt(level x levelMultiplier), above the same floor.</summary>
    [Fact]
    public void SecondaryPoolCoefficientsAreUnchanged()
    {
        var pool = Export.Loaded.Vocabulary.StatCalculation.SecondaryPool;

        Assert.NotNull(pool);
        Assert.Equal(15, pool!.MinimumItemLevel);
        Assert.Equal(72, pool.LevelMultiplier);
        Assert.Equal(2.4, pool.Multiplier);
    }

    /// <summary>
    /// A rarity grants an item extra slots and does not scale the ones it has, which this constant
    /// being one is what says. A build that changed it would make every magnitude in every tooltip
    /// depend on a rung the calculation currently ignores.
    /// </summary>
    [Fact]
    public void RarityStillDoesNotScaleMagnitudes() =>
        Assert.Equal(1, Export.Loaded.Vocabulary.StatCalculation.StatRarityBase);

    /// <summary>
    /// The pools have to rise with item level, which the shape of the formula guarantees and a
    /// misread literal would not. A growth base under one, or a negative multiplier landing in the
    /// wrong position, would still compute and would still export.
    /// </summary>
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
