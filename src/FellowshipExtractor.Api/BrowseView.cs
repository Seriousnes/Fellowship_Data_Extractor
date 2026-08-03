namespace FellowshipExtractor.Api;

/// <summary>
/// The results page as the component draws it: the links off every facet value, sort heading and
/// page number, and the few things worth deciding once rather than inside a loop.
///
/// The whole page is a function of the query string, so a reader's filter is a URL they can send
/// somebody and every link the page draws - a facet value, a sort column, the next page - is that
/// same query string with one thing changed. Nothing is held in the session and nothing is posted.
///
/// It is a plain object rather than anything the framework knows about because none of this needs a
/// request: given a query and its results it can be built, tested and reasoned about on its own.
/// </summary>
public sealed class BrowseView(Codex codex, CodexQuery query, CodexResults results)
{
    public Codex Codex => codex;

    public CodexQuery Query => query;

    public CodexResults Results => results;

    /// <summary>This query with one facet value turned on or off, as a link.</summary>
    public string Toggle(string facet, string value) =>
        "/browse" + query.ToQueryString(toggle: (facet, value));

    public string Replace(string key, string? value) =>
        "/browse" + query.ToQueryString(replace: (key, value));

    public string PageLink(int page) => "/browse" + query.ToQueryString(page: page);

    /// <summary>
    /// The link a column heading carries: sort by it, or reverse it when it is already the sort.
    /// </summary>
    public string SortLink(string column) =>
        query.Sort == column && !query.Descending
            ? "/browse" + query.ToQueryString(replace: ("dir", "desc"))
            : "/browse" + (query with { Descending = false }).ToQueryString(replace: ("sort", column));

    public string? SortState(string column) =>
        query.Sort != column ? null : query.Descending ? "descending" : "ascending";

    /// <summary>Everything but the filters, for the "clear all" link.</summary>
    public string Bare =>
        query.Text is null ? "/browse" : "/browse?q=" + Uri.EscapeDataString(query.Text);

    /// <summary>
    /// The measure columns worth drawing for what is on screen: a column every row leaves empty is
    /// noise, so cooldown appears when abilities are listed and disappears when items are.
    /// </summary>
    public IReadOnlyList<Measure> Columns =>
    [
        .. codex.Measures.Where(m =>
            m.Column && (m.Key == "id" || results.Rows.Any(row => m.Value(row) is not null))),
    ];

    /// <summary>The facets that still have something to say about the rows on screen.</summary>
    public IReadOnlyList<FacetView> Rail =>
    [
        .. results.Facets.Where(facet => facet.Values.Count > 1 || query.Selections.ContainsKey(facet.Key)),
    ];

    /// <summary>
    /// The measures worth offering a range for: the ones something on screen actually carries. A
    /// cooldown box over a page of items is a box that can only empty the page.
    /// </summary>
    public IReadOnlyList<Measure> Ranges =>
    [
        .. codex.Measures.Where(measure =>
            query.Ranges.ContainsKey(measure.Key) ||
            results.Measured.Contains(measure.Key, StringComparer.OrdinalIgnoreCase)),
    ];

    /// <summary>
    /// The class a chosen facet value carries, or nothing.
    ///
    /// Written as a method rather than inline, because Razor mis-parses an empty string literal
    /// inside a quoted attribute: <c>class="@(chosen ? "" : null)"</c> ends the attribute at the
    /// first quote of the literal and emits the attribute on every row, checking every value in the
    /// filter panel at once. Measured, not guessed - that is what the panel did.
    /// </summary>
    public static string? On(bool chosen) => chosen ? "on" : null;

    /// <summary>The page numbers worth offering: both ends, and a window round where the reader is.</summary>
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

    /// <summary>
    /// Every narrowing currently in force, as removable chips.
    ///
    /// A chip is the one place a value the reader chose is drawn back at them, so both halves of it
    /// go through the public spelling: the type reads as its heading and the href carries its slug,
    /// which <see cref="CodexQuery.ToQueryString"/> writes from the table held here.
    /// </summary>
    public IReadOnlyList<(string Label, string Href)> Chosen
    {
        get
        {
            var chips = new List<(string, string)>();

            foreach (var table in query.Tables)
            {
                chips.Add((Codex.Label(table), "/browse" + query.ToQueryString(toggle: ("type", table))));
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
                    (not null, not null) => $"{Codex.Number(range.Min!.Value)} to {Codex.Number(range.Max!.Value)}",
                    (not null, null) => $"from {Codex.Number(range.Min!.Value)}",
                    (null, not null) => $"up to {Codex.Number(range.Max!.Value)}",
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
