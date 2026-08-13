using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class BlessingSlotTests
{
    private static IReadOnlyList<BlessingDocument> Blessings =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<BlessingDocument>()];

    [Fact]
    public void EveryBlessingStatesARankCeilingItsPoolsAndItsPerHeroIds()
    {
        Assert.NotEmpty(Blessings);

        foreach (var blessing in Blessings)
        {
            Assert.True(blessing.MaxRank > 0, $"Blessing {blessing.Id} states no MaxRank.");
            Assert.NotEmpty(blessing.Slots!);
            Assert.NotEmpty(blessing.AbilityRanks!);
        }
    }

    [Fact]
    public void EveryPoolSlotIsASlotTheSettingsWeigh()
    {
        var known = Export.Loaded.Settings.StatCalculation.SlotWeights.Keys;

        foreach (var blessing in Blessings)
        {
            Assert.All(blessing.Slots!, slot => Assert.Contains(slot, known, StringComparer.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void EveryAbilityRankIsKeyedOnAHeroCodenameTheSettingsCarry()
    {
        var codenames = Export.Loaded.Settings.Heroes.Select(hero => hero.Codename).ToArray();

        foreach (var blessing in Blessings)
        {
            Assert.All(blessing.AbilityRanks!, pair =>
            {
                Assert.Contains(pair.Key, codenames, StringComparer.OrdinalIgnoreCase);
                Assert.True(pair.Value > 0);
            });
        }
    }

    [Fact]
    public void OneBlessingCarriesADifferentAbilityRankIdPerHero()
    {
        var spread = Blessings.First(blessing => blessing.AbilityRanks!.Count > 1);

        Assert.Equal(
            spread.AbilityRanks!.Count,
            spread.AbilityRanks!.Values.Distinct().Count());
    }
}
