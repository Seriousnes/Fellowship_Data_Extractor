using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed class BrowseView(ICodexService codex, CodexQuery query, CodexResults results)
{
    public ICodexService Codex => codex;

    public CodexQuery Query => query;

    public CodexResults Results => results;

    public string Toggle(string facet, string value) =>
        "/browse" + query.ToQueryString(toggle: (facet, value));

    public string Replace(string key, string? value) =>
        "/browse" + query.ToQueryString(replace: (key, value));

    public string PageLink(int page) => "/browse" + query.ToQueryString(page: page);

    public string SortLink(string column) =>
        query.Sort == column && !query.Descending
            ? "/browse" + query.ToQueryString(replace: ("dir", "desc"))
            : "/browse" + (query with { Descending = false }).ToQueryString(replace: ("sort", column));

    public string? SortState(string column) =>
        query.Sort != column ? null : query.Descending ? "descending" : "ascending";

    public string Bare =>
        query.Text is null ? "/browse" : "/browse?q=" + Uri.EscapeDataString(query.Text);

    public IReadOnlyList<FacetView> Rail =>
    [
        .. results.Facets.Where(facet => facet.Values.Count > 1 || query.Selections.ContainsKey(facet.Key)),
    ];

    public IReadOnlyList<Measure> Ranges =>
    [
        .. codex.Measures.Where(measure =>
            query.Ranges.ContainsKey(measure.Key) ||
            results.Measured.Contains(measure.Key, StringComparer.OrdinalIgnoreCase)),
    ];

    public static string? On(bool chosen) => chosen ? "on" : null;

    public static IEnumerable<int> Window(int current, int last)
    {
        var wanted = new SortedSet<int> { 1, last };
        for (var at = current - 2; at <= current + 2; at++)
        {
            if (at >= 1 && at <= last) wanted.Add(at);
        }

        var previous = 0;
        foreach (var page in wanted)
        {
            if (previous > 0 && page > previous + 1) yield return -1;
            yield return page;
            previous = page;
        }
    }

    public IReadOnlyList<(string Label, string Href)> Chosen
    {
        get
        {
            var chips = new List<(string, string)>();

            foreach (var type in query.Types)
            {
                var slug = EntityTypes.Slug(type);
                chips.Add((Models.FellowshipCodex.Label(type), "/browse" + query.ToQueryString(toggle: ("type", slug))));
            }

            foreach (var (key, values) in query.Selections)
            {
                if (codex.Facet(key) is not { } facet) continue;

                foreach (var value in values)
                {
                    chips.Add(($"{facet.Label}: {codex.Printed(facet, value)}", Toggle(key, value)));
                }
            }

            foreach (var (key, wanted) in query.Flags)
            {
                if (codex.Flag(key) is not { } flag) continue;
                chips.Add(((wanted ? string.Empty : "Not: ") + flag.Label,
                    "/browse" + query.ToQueryString(replace: (key, null))));
            }

            foreach (var (key, range) in query.Ranges)
            {
                if (codex.Measure(key) is not { } measure) continue;

                var bounds = range switch
                {
                    (not null, not null) => $"{Models.FellowshipCodex.Number(range.Min!.Value)} to {Models.FellowshipCodex.Number(range.Max!.Value)}",
                    (not null, null) => $"from {Models.FellowshipCodex.Number(range.Min!.Value)}",
                    (null, not null) => $"up to {Models.FellowshipCodex.Number(range.Max!.Value)}",
                    _ => null,
                };

                if (bounds is null) continue;

                var without = query with
                {
                    Ranges = query.Ranges
                        .Where(r => !string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase),
                };

                chips.Add(($"{measure.Label}: {bounds}{measure.Unit}", "/browse" + without.ToQueryString(page: 1)));
            }

            return chips;
        }
    }
}
