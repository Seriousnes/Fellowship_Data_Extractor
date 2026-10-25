using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed class BrowseView(ICodexService codex, CodexQuery query, CodexResults results, string where)
{
    public ICodexService Codex => codex;

    public CodexQuery Query => query;

    public CodexResults Results => results;

    public string Toggle(string facet, string value) =>
        where + query.ToQueryString(toggle: (facet, value));

    public string Replace(string key, string? value) =>
        where + query.ToQueryString(replace: (key, value));

    public string PageLink(int page) => where + query.ToQueryString(page: page);

    public string SortLink(string column) =>
        query.Sort == column && !query.Descending
            ? where + query.ToQueryString(replace: ("dir", "desc"))
            : where + (query with { Descending = false }).ToQueryString(replace: ("sort", column));

    public string? SortState(string column) =>
        query.Sort != column ? null : query.Descending ? "descending" : "ascending";

    public string Where => where;

    public string Bare =>
        query.Text is null ? where : where + "?q=" + Uri.EscapeDataString(query.Text);

    public IReadOnlyList<FacetView> Rail =>
    [
        .. results.Facets.Where(facet => facet.Values.Count > 1 || query.Selections.ContainsKey(facet.Key)),
    ];

    public IReadOnlyList<FacetView> Primary => [.. Rail.Where(facet => codex.Facet(facet.Key)?.Primary == true)];

    public IReadOnlyList<FacetView> More => [.. Rail.Where(facet => codex.Facet(facet.Key)?.Primary != true)];

    public IReadOnlyList<Flag> Flags => [.. Offered.Where(flag => !flag.Hides)];

    public IReadOnlyList<Flag> Reveals => [.. Offered.Where(flag => flag.Hides)];

    private IEnumerable<Flag> Offered =>
        codex.Flags.Where(flag =>
            query.Flags.ContainsKey(flag.Key) ||
            results.Flagged.Contains(flag.Key, StringComparer.OrdinalIgnoreCase));

    public bool Revealed(string flag) => query.Flags.GetValueOrDefault(flag);

    public bool Selected(string facet) =>
        query.Selections.TryGetValue(facet, out var values) && values.Count > 0;

    public bool Stated(string flag) => query.Flags.ContainsKey(flag);

    public bool Bounded(string measure) => query.Ranges.ContainsKey(measure);

    public bool? Answer(string flag) =>
        query.Flags.TryGetValue(flag, out var wanted) ? wanted : null;

    public string Unbound(string measure)
    {
        var without = query with
        {
            Ranges = query.Ranges
                .Where(r => !string.Equals(r.Key, measure, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase),
        };

        return where + without.ToQueryString(page: 1);
    }

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
                chips.Add((Models.FellowshipCodex.Label(type), where + query.ToQueryString(toggle: ("type", slug))));
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
                if (codex.Flag(key) is not { } flag || (flag.Hides && !wanted)) continue;
                chips.Add(((wanted ? string.Empty : "Not: ") + flag.Label,
                    where + query.ToQueryString(replace: (key, null))));
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

                chips.Add(($"{measure.Label}: {bounds}{measure.Unit}", Unbound(key)));
            }

            return chips;
        }
    }
}
