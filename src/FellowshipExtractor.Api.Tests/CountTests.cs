namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CountTests(Export export)
{
    [Fact]
    public void The_store_counts_rows_and_the_codex_counts_ids()
    {
        var ids = export.Store.Entities
            .Select(document => (EntityTypes.TypeOf(document), document.Id))
            .Distinct()
            .Count();

        Assert.Equal(export.Store.Entities.Count, export.Store.Count);
        Assert.Equal(ids, export.Store.Ids);
        Assert.Equal(ids, export.View.Codex.Count);
        Assert.True(export.Store.Ids < export.Store.Count);
    }

    [Fact]
    public void Every_per_type_count_counts_ids()
    {
        foreach (var type in export.View.Codex.Types)
        {
            var ids = export.Store.Entities
                .Where(document => EntityTypes.TypeOf(document) == type.Type)
                .Select(document => document.Id)
                .Distinct()
                .Count();

            Assert.Equal(ids, type.Count);
        }

        Assert.Equal(export.View.Codex.Count, export.View.Codex.Types.Sum(type => type.Count));
    }
}
