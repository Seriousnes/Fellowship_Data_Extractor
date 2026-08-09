using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface ICodexSearchService
{
    CodexResults Run(CodexQuery query);
}
public sealed class CodexSearchService(
    IEntityStoreService store,
    ICodexService codex,
    ISearchIndexService index,
    ICodexViewService view) : ICodexSearchService
{
    public CodexResults Run(CodexQuery query)
    {
        IEnumerable<EntityDocument> read = query.Text is { } text
            ? index.Search(text, int.MaxValue).Select(hit => hit.Document)
            : store.Entities;

        var rows = read.ToArray();

        var matched = rows.Where(row => Matches(row, query)).ToArray();

        var ordered = Fold(Order(matched, query));

        var pages = Math.Max(1, (ordered.Length + query.PageSize - 1) / query.PageSize);
        var page = Math.Min(query.Page, pages);
        var window = ordered.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToArray();

        var facets = codex.Facets
            .Select(facet => codex.View(facet, Without(rows, query, facet.Key)))
            .Where(view => view.Values.Count > 0)
            .ToArray();

        var across = rows.Where(row => Matches(row, query, skipTypes: true)).ToArray();

        var types = across
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Id).Distinct().Count());

        var acrossTypes = across.Select(row => (EntityTypes.TypeOf(row), row.Id)).Distinct().Count();

        var unbounded = rows.Where(row => Matches(row, query with { Ranges = NoRanges })).ToArray();

        var measured = codex.Measures
            .Where(measure => unbounded.Any(row => measure.Value(row) is not null))
            .Select(measure => measure.Key)
            .ToArray();

        return new CodexResults(ordered.Length, page, pages, window, facets, types, acrossTypes, measured);
    }

    private Listing[] Fold(EntityDocument[] rows) =>
    [
        .. rows.GroupBy(row => (EntityTypes.TypeOf(row), row.Id))
            .Select(readings => Listed(readings.First())),
    ];

    private Listing Listed(EntityDocument shown)
    {
        var type = EntityTypes.TypeOf(shown);
        var drawn = view.Name(shown);

        var variants = store.All(type, shown.Id)
            .GroupBy(view.Name, StringComparer.Ordinal)
            .Where(readings => !string.Equals(readings.Key, drawn, StringComparison.Ordinal))
            .Select(readings => Variant(type, shown.Id, readings.Key, [.. readings]))
            .ToArray();

        return new Listing(shown, variants);
    }

    private Listing.Variant Variant(EntityType type, int id, string name, EntityDocument[] readings)
    {
        var heroes = readings
            .Select(reading => store.Wearer(reading)?.Name)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var address = view.Href(type, id);

        return new Listing.Variant(
            name,
            (readings[0] as ItemDocument)?.Icon,
            CodexView.Tier(readings[0]),
            heroes is [var first, ..] ? $"{address}?hero={Uri.EscapeDataString(first)}" : address,
            heroes is [{ } one] ? view.HeroColour(one) : null);
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

        IOrderedEnumerable<EntityDocument> ordered = query.Sort switch
        {
            "name" => query.Descending
                ? rows.OrderByDescending(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase),

            "type" => query.Descending
                ? rows.OrderByDescending(row => PublicTypes.Label(EntityTypes.TypeOf(row)), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => PublicTypes.Label(EntityTypes.TypeOf(row)), StringComparer.OrdinalIgnoreCase),

            _ => Measured(rows, codex.Measure(query.Sort)!, query.Descending),
        };

        return
        [
            .. ordered
                .ThenBy(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Id),
        ];
    }

    private static IOrderedEnumerable<EntityDocument> Measured(EntityDocument[] rows, Measure measure, bool descending) =>
        descending
            ? rows.OrderBy(row => measure.Value(row) is null).ThenByDescending(row => measure.Value(row))
            : rows.OrderBy(row => measure.Value(row) is null).ThenBy(row => measure.Value(row));
}
