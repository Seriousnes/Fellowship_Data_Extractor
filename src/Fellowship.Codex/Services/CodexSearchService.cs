using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ICodexSearchService
{
    CodexResults Run(CodexQuery query);
}
public sealed class CodexSearchService(
    IEntityStoreService store,
    ICodexService codex,
    ISearchIndexService index) : ICodexSearchService
{
    public CodexResults Run(CodexQuery query)
    {
        var found = query.Text is { } text
            ? index.Search(text, int.MaxValue)
                .Select(hit => hit.Document)
                .Where(row => PublicTypes.IsListed(row))
                .ToArray()
            : Listed();

        var rows = query.Category is { } category
            ? found.Where(row => PublicTypes.Category(EntityTypes.TypeOf(row)) == category).ToArray()
            : found;

        var across = rows.Where(row => Matches(row, query, skipTypes: true)).ToArray();

        var applied = query.Types.Count == 0
            ? across
            : across.Where(row => query.Types.Contains(EntityTypes.TypeOf(row))).ToArray();

        var chosen = query.Types.Count == 1 ? query.Types[0] : (EntityType?)null;

        var ordered = Order(applied, query);

        var pages = Math.Max(1, (ordered.Length + query.PageSize - 1) / query.PageSize);
        var page = Math.Min(query.Page, pages);
        var window = ordered.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToArray();

        var facets = codex.Facets
            .Select(facet => codex.View(
                facet, Narrowed(query, facet.Key) ? Without(rows, query, facet.Key) : applied))
            .Where(view => view.Values.Count > 0)
            .ToArray();

        var types = across
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Count());

        var unbounded = query.Ranges.Count == 0
            ? applied
            : rows.Where(row => Matches(row, query with { Ranges = NoRanges })).ToArray();

        var measured = codex.Measures
            .Where(measure => unbounded.Any(row => measure.Value(row) is not null))
            .Select(measure => measure.Key)
            .ToArray();

        var flagged = codex.Flags
            .Where(flag => flag.Hides
                ? query.Flags.ContainsKey(flag.Key) || rows.Any(flag.Predicate)
                : unbounded.Any(flag.Predicate))
            .Select(flag => flag.Key)
            .ToArray();

        return new CodexResults(ordered.Length, page, pages, window, facets, types, chosen, measured, flagged);
    }

    private static readonly IReadOnlyDictionary<string, (double?, double?)> NoRanges =
        new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);

    private EntityDocument[]? listed;

    private EntityDocument[] Listed() =>
        listed ??= [.. store.Entities.Where(row => PublicTypes.IsListed(row))];

    private static bool Narrowed(CodexQuery query, string facet) =>
        query.Selections.TryGetValue(facet, out var values) && values.Count > 0;

    private EntityDocument[] Without(IReadOnlyList<EntityDocument> rows, CodexQuery query, string facet) =>
        [.. rows.Where(row => Matches(row, query, skipFacet: facet))];

    private bool Matches(EntityDocument row, CodexQuery query, string? skipFacet = null, bool skipTypes = false)
    {
        if (!skipTypes && query.Types.Count > 0 && !query.Types.Contains(EntityTypes.TypeOf(row)))
        {
            return false;
        }

        foreach (var (key, values) in query.Selections)
        {
            if (values.Count == 0) continue;
            if (string.Equals(key, skipFacet, StringComparison.OrdinalIgnoreCase)) continue;
            if (codex.Facet(key) is not { } facet) continue;
            if (!facet.Matches(row, values)) return false;
        }

        foreach (var (key, wanted) in query.Flags)
        {
            if (codex.Flag(key) is not { Hides: false } flag) continue;
            if (flag.Predicate(row) != wanted) return false;
        }

        foreach (var flag in codex.Flags)
        {
            if (!query.Flags.GetValueOrDefault(flag.Key) && !flag.Admits(row)) return false;
        }

        foreach (var (key, range) in query.Ranges)
        {
            if (codex.Measure(key) is not { } measure) continue;

            if (measure.Value(row) is not { } value) return false;
            if (range.Min is { } min && value < min) return false;
            if (range.Max is { } max && value > max) return false;
        }

        return true;
    }

    private EntityDocument[] Order(EntityDocument[] rows, CodexQuery query)
    {
        if (query.Group is not null && codex.Facet(query.Group) is { } grouping)
        {
            return
            [
                .. Order(rows, query with { Group = null })
                    .OrderBy(row => grouping.Values(row).FirstOrDefault() ?? "￿", StringComparer.OrdinalIgnoreCase),
            ];
        }

        if (query.Text is not null && query.Sort == "name" && !query.Descending) return rows;

        var ordered = codex.Sorting(query.Sort) is { } sorting
            ? Sorted(rows, sorting, query.Descending)
            : Measured(rows, codex.Measure(query.Sort)!, query.Descending);

        return
        [
            .. ordered
                .ThenBy(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Id),
        ];
    }

    private static IOrderedEnumerable<EntityDocument> Sorted(
        EntityDocument[] rows, Sorting sorting, bool descending) =>
        descending
            ? rows.OrderBy(row => sorting.Value(row) is null)
                .ThenByDescending(row => sorting.Value(row), SortOrder.Reading)
            : rows.OrderBy(row => sorting.Value(row) is null)
                .ThenBy(row => sorting.Value(row), SortOrder.Reading);

    private static IOrderedEnumerable<EntityDocument> Measured(EntityDocument[] rows, Measure measure, bool descending) =>
        descending
            ? rows.OrderBy(row => measure.Value(row) is null).ThenByDescending(row => measure.Value(row))
            : rows.OrderBy(row => measure.Value(row) is null).ThenBy(row => measure.Value(row));
}
