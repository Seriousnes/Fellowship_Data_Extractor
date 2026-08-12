namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CountTests(Export export)
{
    [Fact]
    public void The_store_counts_rows_and_the_codex_counts_the_listed_ones()
    {
        var ids = export.Store.Entities
            .Select(document => (EntityTypes.TypeOf(document), document.Id))
            .Distinct()
            .Count();

        var listed = export.Store.Entities.Count(row => PublicTypes.Listed(EntityTypes.TypeOf(row)));

        Assert.Equal(export.Store.Entities.Count, export.Store.Count);
        Assert.Equal(ids, export.Store.Ids);
        Assert.Equal(listed, export.View.Codex.Count);
        Assert.True(export.View.Codex.Count < export.Store.Count);
    }

    [Fact]
    public void Every_per_type_count_counts_documents()
    {
        foreach (var type in export.View.Codex.Types)
        {
            var rows = export.Store.Entities.Count(document => EntityTypes.TypeOf(document) == type.Type);

            Assert.Equal(rows, type.Count);
        }

        Assert.Equal(export.View.Codex.Count, export.View.Codex.Types.Sum(type => type.Count));
    }
}
