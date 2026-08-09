using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class SuggestionsTests(Export export)
{
    private ISuggestionService Box =>
        new SuggestionService(
            new SearchIndexService(export.Store, NullLogger<SearchIndexService>.Instance), export.View);

    [Fact]
    public void A_word_matching_many_readings_of_one_id_suggests_it_once()
    {
        var found = Box.For("Betrayer's", null, 50);
        var ids = found.Results.Select(hit => (hit.Type, hit.Id)).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(ids.Length, found.Total);
    }

    [Fact]
    public void The_per_type_counts_count_ids()
    {
        var found = Box.For("Betrayer's", null, 200);

        Assert.Equal(found.Total, found.Groups.Sum(group => group.Count));
    }
}
