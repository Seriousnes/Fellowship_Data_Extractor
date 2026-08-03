using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FellowshipExtractor.Api.Pages;

/// <summary>
/// The results page: type tabs, the filter rail, and the table of everything the filter left.
///
/// The whole page is a function of the query string, so a reader's filter is a URL they can send
/// somebody and every link the page draws - a facet value, a sort column, the next page - is that
/// same query string with one thing changed. Nothing is held in the session and nothing is posted.
/// </summary>
public sealed class BrowseModel(Codex codex, CodexSearch search) : PageModel
{
    public CodexQuery Query { get; private set; } = new();

    public CodexResults Results { get; private set; } =
        new(0, 1, 1, [], [], new Dictionary<string, int>(), 0, []);

    public string? Error { get; private set; }

    public Codex Codex => codex;

    public IActionResult OnGet()
    {
        Query = CodexQuery.Parse(Request.Query, codex, out var error);
        Error = error;

        // A GET form posts every one of its inputs, so applying one range arrives carrying twenty
        // empty ones. They parse to nothing and change no result, but the address is what a reader
        // copies and sends, so the canonical form of the query is what the page settles on.
        if (error is null && Request.Query.Any(p => p.Value.All(string.IsNullOrEmpty)))
        {
            return Redirect("/browse" + Query.ToQueryString());
        }

        if (error is null) Results = search.Run(Query);

        return Page();
    }

    /// <summary>This query with one facet value turned on or off, as a link.</summary>
    public string Toggle(string facet, string value) =>
        "/browse" + Query.ToQueryString(toggle: (facet, value));

    public string Replace(string key, string? value) =>
        "/browse" + Query.ToQueryString(replace: (key, value));

    public string PageLink(int page) => "/browse" + Query.ToQueryString(page: page);

    /// <summary>
    /// The link a column heading carries: sort by it, or reverse it when it is already the sort.
    /// </summary>
    public string SortLink(string column) =>
        Query.Sort == column && !Query.Descending
            ? "/browse" + Query.ToQueryString(replace: ("dir", "desc"))
            : "/browse" + (Query with { Descending = false }).ToQueryString(replace: ("sort", column));

    public string? SortState(string column) =>
        Query.Sort != column ? null : Query.Descending ? "descending" : "ascending";

    /// <summary>
    /// The measure columns worth drawing for what is on screen: a column every row leaves empty is
    /// noise, so cooldown appears when abilities are listed and disappears when items are.
    /// </summary>
    public IReadOnlyList<Measure> Columns =>
    [
        .. codex.Measures.Where(m => m.Column && (m.Key == "id" || Results.Rows.Any(row => m.Value(row) is not null))),
    ];

    /// <summary>The facets that still have something to say about the rows on screen.</summary>
    public IReadOnlyList<FacetView> Rail =>
    [
        .. Results.Facets.Where(facet => facet.Values.Count > 1 || Query.Selections.ContainsKey(facet.Key)),
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

    /// <summary>Every narrowing currently in force, as removable chips.</summary>
    public IReadOnlyList<(string Label, string Href)> Chosen
    {
        get
        {
            var chips = new List<(string, string)>();

            foreach (var table in Query.Tables)
            {
                chips.Add((Codex.Label(table), "/browse" + Query.ToQueryString(toggle: ("type", table))));
            }

            foreach (var (key, values) in Query.Selections)
            {
                if (codex.Facet(key) is not { } facet) continue;

                foreach (var value in values)
                {
                    chips.Add(($"{facet.Label}: {Codex.Spaced(value)}", Toggle(key, value)));
                }
            }

            foreach (var (key, wanted) in Query.Flags)
            {
                if (codex.Flag(key) is not { } flag) continue;
                chips.Add(((wanted ? string.Empty : "Not: ") + flag.Label,
                    "/browse" + Query.ToQueryString(replace: (key, null))));
            }

            foreach (var (key, range) in Query.Ranges)
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

                var without = Query with
                {
                    Ranges = Query.Ranges
                        .Where(r => !string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase),
                };

                chips.Add(($"{measure.Label}: {bounds}{measure.Unit}", "/browse" + without.ToQueryString(page: 1)));
            }

            return chips;
        }
    }

    /// <summary>
    /// The measures worth offering a range for: the ones something on screen actually carries. A
    /// cooldown box over a page of items is a box that can only empty the page.
    /// </summary>
    public IReadOnlyList<Measure> Ranges =>
    [
        .. codex.Measures.Where(measure =>
            Query.Ranges.ContainsKey(measure.Key) ||
            Results.Measured.Contains(measure.Key, StringComparer.OrdinalIgnoreCase)),
    ];
}
