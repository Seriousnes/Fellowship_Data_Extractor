namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NamedTests(Export export)
{
    [Fact]
    public void A_bare_browse_answers_only_rows_the_build_names()
    {
        var results = Searching.Run(export);

        Assert.Equal(EntityType.Item, results.Chosen);
        Assert.Equal(2684, results.Total);
        Assert.All(results.Rows, row => Assert.NotNull(PublicTypes.DisplayNameOf(row)));
    }

    [Fact]
    public void Asking_for_the_unnamed_answers_the_others()
    {
        var results = Searching.Run(export, ("type", "item"), ("named", "0"));

        Assert.Equal(3060, results.Total);
        Assert.All(results.Rows, row => Assert.Null(PublicTypes.DisplayNameOf(row)));
    }

    [Fact]
    public void The_two_halves_sum_to_every_reading_of_every_item()
    {
        var named = Searching.Run(export, ("type", "item")).Total;
        var unnamed = Searching.Run(export, ("type", "item"), ("named", "0")).Total;

        Assert.Equal(5744, named + unnamed);
    }

    [Fact]
    public void A_tab_count_equals_the_total_of_the_list_it_opens()
    {
        var tabs = Searching.Run(export).Types;

        foreach (var (type, counted) in tabs)
        {
            Assert.Equal(counted, Searching.Run(export, ("type", EntityTypes.Slug(type))).Total);
        }
    }

    [Fact]
    public void The_codex_counts_every_document_of_every_listed_type()
    {
        var listed = export.Store.Entities.Count(row => PublicTypes.Listed(EntityTypes.TypeOf(row)));

        Assert.Equal(12231, listed);
        Assert.Equal(listed, export.View.Codex.Count);
        Assert.Equal(listed, export.View.Codex.Types.Sum(type => type.Count));
    }

    [Fact]
    public void An_id_typed_into_the_omnibox_answers_the_documents_carrying_it()
    {
        var hits = Searching.Index(export).Search("5191", 50);

        Assert.NotEmpty(hits);
        Assert.All(hits.TakeWhile(hit => hit.Score == SearchIndexService.IdScore),
            hit => Assert.Equal(5191, hit.Document.Id));

        Assert.Contains(hits, hit => hit.Document is ItemDocument { Id: 5191 });
    }

    [Fact]
    public void An_unnamed_row_is_reachable_by_the_one_thing_it_has()
    {
        var unnamed = export.Store.Entities.First(row =>
            PublicTypes.Listed(EntityTypes.TypeOf(row)) && PublicTypes.DisplayNameOf(row) is null);

        var hits = Searching.Index(export)
            .Search(unnamed.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), 200);

        Assert.Contains(hits, hit =>
            hit.Document.Id == unnamed.Id && EntityTypes.TypeOf(hit.Document) == EntityTypes.TypeOf(unnamed));
    }
}
