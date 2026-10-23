using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class LegendaryStatTests
{
    private static IReadOnlyList<ItemDocument> Legendaries =>
    [
        .. EntityStoreCorpusTests.Store.Entities.OfType<ItemDocument>()
            .Where(item => item.LegendaryStats is not null || item.UntaggedLegendaryStats is not null),
    ];

    [Fact]
    public void ALegendaryItemCarriesEveryLegendaryStatItsDefinitionNames()
    {
        Assert.NotEmpty(Legendaries);

        Assert.All(Legendaries, item =>
            Assert.Equal(3, (item.LegendaryStats?.Count ?? 0) + (item.UntaggedLegendaryStats?.Count ?? 0)));
    }

    [Fact]
    public void BloodOfTheUndyingIsALegendaryStatEveryLegendaryItemNames()
    {
        var blood = EntityStoreCorpusTests.Store.Entities.OfType<LegendaryStatDocument>()
            .Where(stat => stat.Name == "Blood of the Undying")
            .Select(stat => stat.Id)
            .ToHashSet();

        Assert.NotEmpty(blood);

        Assert.All(Legendaries, item =>
        {
            Assert.Null(item.UntaggedLegendaryStats);
            Assert.Contains(item.LegendaryStats!, stat => blood.Contains(stat.Id));
        });
    }
}
