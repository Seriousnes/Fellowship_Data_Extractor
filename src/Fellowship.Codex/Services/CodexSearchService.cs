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
        IEnumerable<EntityDocument> read = query.Text is { } text
            ? index.Search(text, int.MaxValue).Select(hit => hit.Document)
            : store.Entities;

        var rows = read.Where(row => PublicTypes.Listed(EntityTypes.TypeOf(row))).ToArray();

        var across = rows.Where(row => Matches(row, query, skipTypes: true)).ToArray();

        var chosen = query.Types.Count > 0 ? query.Types : [Default(across)];

        var matched = across.Where(row => chosen.Contains(EntityTypes.TypeOf(row))).ToArray();

        var ordered = Order(matched, query);

        var pages = Math.Max(1, (ordered.Length + query.PageSize - 1) / query.PageSize);
        var page = Math.Min(query.Page, pages);
        var window = ordered.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToArray();

        var facets = codex.Facets
            .Select(facet => codex.View(facet, Without(rows, query, facet.Key)))
            .Where(view => view.Values.Count > 0)
            .ToArray();

        var types = across
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Count());

        var unbounded = rows.Where(row => Matches(row, query with { Ranges = NoRanges })).ToArray();

        var measured = codex.Measures
            .Where(measure => unbounded.Any(row => measure.Value(row) is not null))
            .Select(measure => measure.Key)
            .ToArray();

        return new CodexResults(ordered.Length, page, pages, window, facets, types, chosen[0], measured);
    }

    private static EntityType Default(IReadOnlyList<EntityDocument> across)
    {
        var present = across.Select(EntityTypes.TypeOf).ToHashSet();

        foreach (var tab in PublicTypes.Tabs)
        {
            if (present.Contains(tab.Type)) return tab.Type;
        }

        return EntityType.Item;
    }

    private static readonly IReadOnlyDictionary<string, (double?, double?)> NoRanges =
        new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);

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
            if (codex.Flag(key) is not { } flag) continue;
            if (flag.Predicate(row) != wanted) return false;
        }

        if (!query.StatesNamed &&
            codex.Flag(CodexQuery.NamedFlag) is { } named &&
            !named.Predicate(row))
        {
            return false;
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
