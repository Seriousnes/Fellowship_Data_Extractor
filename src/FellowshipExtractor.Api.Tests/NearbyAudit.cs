namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NearbyAudit(Export export)
{
    [Fact]
    public void No_facet_value_counts_rows_the_list_it_opens_does_not_draw()
    {
        var lost = new List<string>();

        foreach (var type in export.View.Codex.Types.Select(view => view.Type))
        {
            var rows = export.Store.Entities.Where(row => EntityTypes.TypeOf(row) == type).ToArray();

            foreach (var facet in export.View.Codex.Facets)
            {
                var view = export.View.Codex.View(facet, rows);

                foreach (var value in view.Values)
                {
                    var matched = rows.Count(row => facet.Matches(row, [value.Value]));

                    if (matched != value.Count)
                    {
                        lost.Add($"{EntityTypes.Slug(type)} {facet.Key}={value.Value}: " +
                            $"counted {value.Count}, matches {matched}");
                    }
                }
            }
        }

        Assert.True(lost.Count == 0, string.Join("; ", lost.Take(10)));
    }

    [Fact]
    public void A_more_like_this_badge_states_the_total_of_the_list_it_links_to()
    {
        var facet = export.View.Codex.Facet("slot")!;
        var named = export.View.Codex.Flag(CodexQuery.NamedFlag)!;

        foreach (var value in export.View.Codex.Offered(facet).Take(4))
        {
            var badge = export.Store.Entities.Count(row =>
                EntityTypes.TypeOf(row) == EntityType.Item &&
                facet.Matches(row, [value]) &&
                named.Predicate(row));

            var drawn = Searching.Run(export, ("type", "item"), (facet.Key, value)).Total;

            Assert.Equal(badge, drawn);
        }
    }
}
