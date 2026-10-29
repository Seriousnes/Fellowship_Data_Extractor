using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed class BrowseView(ICodexService codex, CodexQuery query, CodexResults results, string where)
{
    public ICodexService Codex => codex;

    public CodexQuery Query => query;

    public CodexResults Results => results;

    public string Toggle(FacetType facet, string value) => where + query.Toggled(facet, value).ToQueryString();

    public string Choose(FacetType facet, string? value) => where + query.WithFacet(facet, value).ToQueryString();

    public string State(FlagType flag, bool? wanted) => where + query.WithFlag(flag, wanted).ToQueryString();

    public string OfType(EntityType? type) => where + query.WithType(type).ToQueryString();

    public string GroupBy(FacetType? group) => where + query.WithGroup(group).ToQueryString();

    public string PageLink(int page) => where + query.ToQueryString(page: page);

    public string SortLink(SortColumn column) =>
        where + query.WithSort(column, descending: query.Sort == column && !query.Descending).ToQueryString();

    public string SortBy(SortColumn column) =>
        where + query.WithSort(column, descending: false).ToQueryString();

    public string? SortState(SortColumn column) =>
        query.Sort != column ? null : query.Descending ? "descending" : "ascending";

    public string Where => where;

    public string Bare =>
        query.Text is null ? where : where + "?q=" + Uri.EscapeDataString(query.Text);

    public IReadOnlyList<FacetCount> Rail =>
    [
        .. results.Facets.Where(facet => facet.Values.Count > 1 || query.Selections.ContainsKey(facet.Type)),
    ];

    public IReadOnlyList<FacetCount> Primary => [.. Rail.Where(facet => facet.Facet.Primary)];

    public IReadOnlyList<FacetCount> More => [.. Rail.Where(facet => !facet.Facet.Primary)];

    public IReadOnlyList<Flag> Flags => [.. Offered.Where(flag => !flag.Hides)];

    public IReadOnlyList<Flag> Reveals => [.. Offered.Where(flag => flag.Hides)];

    private IEnumerable<Flag> Offered =>
        codex.Flags.Where(flag => query.Flags.ContainsKey(flag.Type) || results.Flagged.Contains(flag.Type));

    public bool Revealed(FlagType flag) => query.Flags.GetValueOrDefault(flag);

    public bool Selected(FacetType facet) =>
        query.Selections.TryGetValue(facet, out var values) && values.Count > 0;

    public bool Stated(FlagType flag) => query.Flags.ContainsKey(flag);

    public bool Bounded(MeasureType measure) => query.Ranges.ContainsKey(measure);

    public bool? Answer(FlagType flag) =>
        query.Flags.TryGetValue(flag, out var wanted) ? wanted : null;

    public string Unbound(MeasureType measure) => where + query.WithoutRange(measure).ToQueryString();

    public IReadOnlyList<Measure> Ranges =>
    [
        .. codex.Measures.Where(measure =>
            query.Ranges.ContainsKey(measure.Type) || results.Measured.Contains(measure.Type)),
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
                chips.Add((Models.FellowshipCodex.Label(type), where + query.Toggled(type).ToQueryString()));
            }

            foreach (var (key, values) in query.Selections)
            {
                var facet = codex.Facet(key);

                foreach (var value in values)
                {
                    chips.Add(($"{facet.Label}: {codex.Printed(facet, value)}", Toggle(key, value)));
                }
            }

            foreach (var (key, wanted) in query.Flags)
            {
                var flag = codex.Flag(key);
                if (flag.Hides && !wanted) continue;
                chips.Add(((wanted ? string.Empty : "Not: ") + flag.Label, State(key, null)));
            }

            foreach (var (key, range) in query.Ranges)
            {
                var measure = codex.Measure(key);

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
