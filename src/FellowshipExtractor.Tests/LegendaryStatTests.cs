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
    public void BloodOfTheUndyingHasNoNetIdAndGoesOnTheItemByName()
    {
        Assert.All(Legendaries, item =>
        {
            var untagged = Assert.Single(item.UntaggedLegendaryStats!);

            Assert.Equal("Blood of the Undying", untagged.Name);
            Assert.Equal("+10% Maximum Health", untagged.Description);
            Assert.InRange(untagged.Position, 0, 2);
        });

        Assert.DoesNotContain(
            EntityStoreCorpusTests.Store.Entities.OfType<LegendaryStatDocument>(),
            stat => stat.Name == "Blood of the Undying");
    }
}
