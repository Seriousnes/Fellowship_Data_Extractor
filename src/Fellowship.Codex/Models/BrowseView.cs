using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

/// <param name="where">
/// The address every link starts from. It may carry a query of its own, which each link keeps ahead of the
/// browse parameters.
/// </param>
/// <param name="pinned">Facets the page fixes, so the rail offers neither their values nor a grouping by them.</param>
/// <param name="listed">Query for <paramref name="results"/>. It differs from <paramref name="query"/> on a page that fixes more filters.</param>
public sealed class BrowseView(
    ICodexService codex,
    CodexQuery query,
    CodexResults results,
    string where,
    IReadOnlySet<FacetType>? pinned = null,
    CodexQuery? listed = null)
{
    /// <summary>Address of the unpaged <paramref name="format"/> file of the list.</summary>
    public string Download(string format)
    {
        var answered = listed ?? query;

        if (answered.Types.Count == 0 && answered.Category is { } category)
        {
            answered = answered with
            {
                Types = [.. PublicTypes.Listed.Where(type => type.Category == category).Select(type => type.Type)],
            };
        }

        return $"/api/entities.{format}" + (answered with { Page = 1 }).ToQueryString();
    }

    public ICodexService Codex => codex;

    public CodexQuery Query => query;

    public CodexResults Results => results;

    public string Toggle(FacetType facet, string value) => At(query.Toggled(facet, value).ToQueryString());

    public string Choose(FacetType facet, string? value) => At(query.WithFacet(facet, value).ToQueryString());

    public string State(FlagType flag, bool? wanted) => At(query.WithFlag(flag, wanted).ToQueryString());

    public string OfType(EntityType? type) => At(query.WithType(type).ToQueryString());

    public string GroupBy(FacetType? group) => At(query.WithGroup(group).ToQueryString());

    public string PageLink(int page) => At(query.ToQueryString(page: page));

    public string SortLink(SortColumn column) =>
        At(query.WithSort(column, descending: query.Sort == column && !query.Descending).ToQueryString());

    public string SortBy(SortColumn column) =>
        At(query.WithSort(column, descending: false).ToQueryString());

    public string? SortState(SortColumn column) =>
        query.Sort != column ? null : query.Descending ? "descending" : "ascending";

    public string Where => where;

    public string Bare =>
        query.Text is null ? where : At("?q=" + Uri.EscapeDataString(query.Text));

    private string At(string browsed) =>
        browsed.Length == 0 || !where.Contains('?', StringComparison.Ordinal) ? where + browsed : where + "&" + browsed[1..];

    public IReadOnlyList<FacetCount> Rail =>
    [
        .. results.Facets.Where(facet =>
            pinned?.Contains(facet.Type) is not true &&
            (facet.Values.Count > 1 || query.Selections.ContainsKey(facet.Type))),
    ];

    /// <summary>Primary facets the rail offers one value of at a time.</summary>
    public IReadOnlyList<FacetCount> Single => [.. Rail.Where(facet => facet.Facet is { Primary: true, Single: true })];

    /// <summary>Primary facets the rail offers several values of.</summary>
    public IReadOnlyList<FacetCount> Primary => [.. Rail.Where(facet => facet.Facet is { Primary: true, Single: false })];

    public IReadOnlyList<FacetCount> More => [.. Rail.Where(facet => !facet.Facet.Primary)];

    public IReadOnlyList<Flag> Flags => [.. Offered.Where(flag => !flag.Hides)];

    public IReadOnlyList<Flag> Reveals => [.. Offered.Where(flag => flag.Hides)];

    private IEnumerable<Flag> Offered =>
        codex.Flags.Where(flag => query.Flags.ContainsKey(flag.Type) || results.Flagged.Contains(flag.Type));

    public bool Revealed(FlagType flag) => query.Flags.GetValueOrDefault(flag);

    public bool Selected(FacetType facet) =>
        query.Selections.TryGetValue(facet, out var values) && values.Count > 0;

    public bool Stated(FlagType flag) => query.Flags.ContainsKey(flag);

    public bool? Answer(FlagType flag) =>
        query.Flags.TryGetValue(flag, out var wanted) ? wanted : null;

    public string Without(Comparison comparison) => At(query.Without(comparison).ToQueryString());

    /// <summary>Offered measures with a value on at least one row of the list, and the measures of the comparisons.</summary>
    public IReadOnlyList<Measure> Comparable =>
    [
        .. codex.Measures.Where(measure => (measure.Offered && results.Measured.Contains(measure.Key)) ||
            query.Comparisons.Any(comparison => comparison.Measure == measure.Key)),
    ];

    /// <summary>Measures in the comparisons, in order of first use. Each has a column in the table.</summary>
    public IReadOnlyList<Measure> Compared =>
    [
        .. query.Comparisons.Select(comparison => comparison.Measure).Distinct().Select(codex.Measure),
    ];

    public static string Printed(Comparator comparator) => CodexKeys.Label(comparator);

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
                chips.Add((Models.FellowshipCodex.Label(type), At(query.Toggled(type).ToQueryString())));
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

            foreach (var comparison in query.Comparisons)
            {
                var measure = codex.Measure(comparison.Measure);

                chips.Add((
                    $"{measure.Label} {Printed(comparison.Comparator)} {Models.FellowshipCodex.Number(comparison.Value)}{measure.Unit}",
                    Without(comparison)));
            }

            return chips;
        }
    }
}
