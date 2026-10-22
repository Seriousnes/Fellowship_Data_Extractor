using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Documents;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class BlessingSlotTests
{
    private const string RankTable =
        "fellowship/Content/Items/DynamicItemAbilityRanks/DT_Generic_DynamicAbilityRank.DT_Generic_DynamicAbilityRank";

    private static IReadOnlyList<BlessingDocument> Blessings =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<BlessingDocument>()];

    private static UDataTable Ranks() => Export.Provider.LoadPackageObject<UDataTable>(RankTable);

    [Fact]
    public void EveryBlessingStatesARankCeilingItsPoolsAndItsRankId()
    {
        Assert.NotEmpty(Blessings);

        foreach (var blessing in Blessings)
        {
            Assert.True(blessing.MaxRank > 0, $"Blessing {blessing.Id} states no MaxRank.");
            Assert.NotEmpty(blessing.Slots!);
            Assert.True(blessing.AbilityRank > 0, $"Blessing {blessing.Id} states no ability rank id.");
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
    public void EveryRankRowIsOneBlessingCarryingItsTagIdAndEveryPool()
    {
        var rankIds = CsvTables.ReadTags(Export.Provider)
            .Where(tag => tag.Table == DynamicAbilityRanks.Table)
            .ToDictionary(tag => tag.Name, tag => tag.NetId);

        var rows = Ranks().RowMap.Values.ToArray();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, Blessings.Count);

        foreach (var row in rows)
        {
            var tag = PropertyWalk.GetString(row, "ID")!;
            var pools = Assert.IsType<UScriptArray>(PropertyWalk.Get(row, "PoolIds")?.GenericValue).Properties.Count;

            var blessing = Assert.Single(Blessings, blessing => blessing.AbilityRank == rankIds[tag]);

            Assert.Equal(pools, blessing.Slots!.Count);
            Assert.Equal(PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "MaxRank")?.GenericValue), blessing.MaxRank);
        }
    }

    [Theory]
    [InlineData(700, "DynamicItemAbilityRank.Generic.02")]
    [InlineData(712, "DynamicItemAbilityRank.Generic.06")]
    public void ABlessingCarriesTheGenericRankIdOfTheRowGrantingIt(int blessing, string rank)
    {
        var expected = CsvTables.ReadTags(Export.Provider)
            .Single(tag => tag.Table == DynamicAbilityRanks.Table && tag.Name == rank)
            .NetId;

        Assert.Equal(expected, Blessings.Single(document => document.Id == blessing).AbilityRank);
    }
}
