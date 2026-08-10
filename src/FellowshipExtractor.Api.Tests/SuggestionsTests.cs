using FellowshipExtractor.Api.Components.Layout;

using Microsoft.AspNetCore.Components.Web;
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

[Collection(Corpus.Name)]
public class OmniboxTests : BunitContext
{
    private const string TwoHits = "acolyte";

    private readonly ISuggestionService Box;

    public OmniboxTests(Export export)
    {
        Box = new SuggestionService(
            new SearchIndexService(export.Store, NullLogger<SearchIndexService>.Instance), export.View);

        Services.AddSingleton(Box);
        Services.AddSingleton(export.View);
    }

    private IRenderedComponent<Omnibox> Dropped()
    {
        var cut = Render<Omnibox>();

        cut.Find("input").Input(TwoHits);
        cut.WaitForElement(".cx-omnibox-drop");

        return cut;
    }

    [Fact]
    public void A_query_whose_hits_all_fit_still_draws_the_view_all_row()
    {
        var found = Box.For(TwoHits, null, 12);

        Assert.Equal(2, found.Total);
        Assert.Equal(found.Total, found.Results.Count);

        var button = Dropped().Find(".cx-omnibox-all");

        Assert.DoesNotContain(button.TextContent, char.IsDigit);
        Assert.Equal("View all results", button.TextContent.Trim());
    }

    [Fact]
    public void Arrow_up_from_the_initial_state_selects_the_view_all_row()
    {
        var cut = Dropped();

        Assert.Null(cut.Find(".cx-omnibox-all").GetAttribute("data-active"));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        Assert.Equal("on", cut.Find(".cx-omnibox-all").GetAttribute("data-active"));
    }
}
