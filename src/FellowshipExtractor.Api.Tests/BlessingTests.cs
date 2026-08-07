namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class BlessingTests(Export export)
{
    [Fact]
    public void The_corpus_reads_blessings_and_no_finesse_survives()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);
        Assert.All(blessings, blessing =>
            Assert.Equal(EntityType.Blessing, EntityTypes.TypeOf(blessing)));

        Assert.Equal("blessing", EntityTypes.Slug(EntityType.Blessing));
        Assert.Equal(typeof(BlessingDocument), EntityTypes.Document(EntityType.Blessing));
    }

    [Fact]
    public void A_blessing_is_printed_and_addressed_as_one()
    {
        var blessing = PublicTypes.All.Single(type => type.Type == EntityType.Blessing);

        Assert.Equal("Blessings", blessing.Label);
        Assert.Equal("blessing", blessing.Noun);
        Assert.Equal("blessing", blessing.Slug);
    }

    [Fact]
    public void Every_blessing_carries_the_art_the_build_gives_a_blessing()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.All(blessings, blessing => Assert.Contains(
            "BlessingRuneIcon", blessing.Icon ?? string.Empty, StringComparison.Ordinal));
    }
}
