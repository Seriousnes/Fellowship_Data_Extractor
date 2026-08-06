namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class NearbyAudit(Export export)
{
    [Fact]
    public void No_facet_value_falls_from_two_rows_to_fewer_than_two_ids()
    {
        var lost = new List<string>();

        foreach (var type in export.View.Codex.Types.Select(view => view.Type))
        {
            var rows = export.Store.Entities.Where(row => EntityTypes.TypeOf(row) == type).ToArray();

            foreach (var facet in export.View.Codex.Facets)
            {
                foreach (var value in rows.SelectMany(facet.Values).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (value.Length == 0) continue;

                    var matched = rows.Where(row => facet.Matches(row, [value])).ToArray();
                    var ids = matched.Select(row => row.Id).Distinct().Count();

                    if (matched.Length >= 2 && ids < 2)
                    {
                        lost.Add($"{EntityTypes.Slug(type)} {facet.Key}={value}: {matched.Length} rows, {ids} ids");
                    }
                }
            }
        }

        Assert.True(lost.Count == 0, string.Join("; ", lost));
    }
}
