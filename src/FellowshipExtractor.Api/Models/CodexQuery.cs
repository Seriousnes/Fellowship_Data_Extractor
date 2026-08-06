using System.Globalization;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record CodexQuery
{
    public const int DefaultPageSize = 50;
    private const int MaximumPageSize = 200;

    public static readonly IReadOnlyList<ApiParameter> Parameters =
    [
        new("q", ApiValue.Text, "Free text matched against every name."),
        new("type", ApiValue.Text, "Entity type slug, comma separated for several."),
        new("sort", ApiValue.Text, "The column to order by. Defaults to name."),
        new("dir", ApiValue.Text, "Given as desc to order the other way."),
        new("group", ApiValue.Text, "The facet to group the page by."),
        new("page", ApiValue.Whole, "Which page to answer, from 1."),
        new("size", ApiValue.Whole, $"How many rows a page holds, up to {MaximumPageSize}. Defaults to {DefaultPageSize}."),
    ];

    public string? Text { get; init; }

    public IReadOnlyList<EntityType> Types { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Selections { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, bool> Flags { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, (double? Min, double? Max)> Ranges { get; init; } =
        new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);

    public string Sort { get; init; } = "name";

    public bool Descending { get; init; }

    public string? Group { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public static CodexQuery Parse(IQueryCollection query, Codex codex, out string? error)
    {
        error = null;

        var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var ranges = new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);
        var types = new List<EntityType>();

        string? text = null, sort = "name", group = null;
        var descending = false;
        var page = 1;
        var pageSize = DefaultPageSize;

        foreach (var (key, raw) in query)
        {
            var values = raw
                .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToArray();

            if (Declared(key) is { } control)
            {
                switch (control)
                {
                    case "q":
                        text = raw.Count > 0 ? raw[^1] : null;
                        break;

                    case "type":
                        foreach (var slug in values)
                        {
                            if (!EntityTypes.TryParse(slug, out var named))
                            {
                                error = PublicTypes.NoSuchType(slug);
                                return new CodexQuery();
                            }

                            types.Add(named);
                        }

                        break;

                    case "sort":
                        if (values.Length > 0) sort = values[^1];
                        break;

                    case "dir":
                        descending = values.Length > 0 &&
                            values[^1].StartsWith('d');
                        break;

                    case "group":
                        group = values.Length > 0 ? values[^1] : null;
                        break;

                    case "page":
                        if (!TryNumber(values, out var wanted, out error)) return new CodexQuery();
                        page = wanted is > 0 ? (int)wanted : 1;
                        break;

                    case "size":
                        if (!TryNumber(values, out var size, out error)) return new CodexQuery();
                        pageSize = size is > 0 ? Math.Min((int)size, MaximumPageSize) : DefaultPageSize;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"'{control}' is declared as a filter parameter and nothing reads it, so a caller " +
                            "would find it in the published document and see it do nothing.");
                }

                continue;
            }

            if (codex.Facet(key) is { } facet)
            {
                var offered = codex.Offered(facet);

                foreach (var value in values)
                {
                    if (offered.Contains(value, StringComparer.OrdinalIgnoreCase)) continue;

                    error = $"'{value}' is not a value of {facet.Label}. The values are " +
                        string.Join(", ", offered) + ".";
                    return new CodexQuery();
                }

                selections[facet.Key] = values;
                continue;
            }

            if (codex.Flag(key) is { } flag)
            {
                var value = values.Length == 0 || !(values[^1] is "0" or "false" or "no");
                flags[flag.Key] = value;
                continue;
            }

            if (Bound(key) is var (measureKey, isMax) && codex.Measure(measureKey) is { } measure)
            {
                if (!TryNumber(values, out var bound, out error)) return new CodexQuery();
                if (bound is null) continue;

                var current = ranges.GetValueOrDefault(measure.Key);
                ranges[measure.Key] = isMax ? (current.Item1, bound) : (bound, current.Item2);
                continue;
            }

            error = $"Unknown parameter '{key}'. The filters are " +
                string.Join(", ", codex.Facets.Select(f => f.Key)
                    .Concat(codex.Flags.Select(f => f.Key))
                    .Concat(codex.Measures.SelectMany(m => new[] { m.Key + "Min", m.Key + "Max" }))
                    .Concat(Parameters.Select(p => p.Name))) + ".";
            return new CodexQuery();
        }

        if (sort is not "name" and not "type" && codex.Measure(sort) is null)
        {
            error = $"'{sort}' is not a column. Sort by name, type or one of " +
                string.Join(", ", codex.Measures.Select(m => m.Key)) + ".";
            return new CodexQuery();
        }

        if (group is not null && codex.Facet(group) is null)
        {
            error = $"'{group}' is not a facet to group by. The facets are " +
                string.Join(", ", codex.Facets.Select(f => f.Key)) + ".";
            return new CodexQuery();
        }

        return new CodexQuery
        {
            Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            Types = types,
            Selections = selections,
            Flags = flags,
            Ranges = ranges,
            Sort = sort,
            Descending = descending,
            Group = group,
            Page = page,
            PageSize = pageSize,
        };
    }

    private static string? Declared(string key) => Parameters
        .FirstOrDefault(parameter => parameter.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.Name;

    private static (string Key, bool IsMax) Bound(string key) =>
        key.EndsWith("Max", StringComparison.OrdinalIgnoreCase) ? (key[..^3], true)
        : key.EndsWith("Min", StringComparison.OrdinalIgnoreCase) ? (key[..^3], false)
        : (key, false);

    private static bool TryNumber(IReadOnlyList<string> values, out double? result, out string? error)
    {
        result = null;
        error = null;

        if (values.Count == 0) return true;

        if (!double.TryParse(values[^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            error = $"'{values[^1]}' is not a number.";
            return false;
        }

        result = number;
        return true;
    }

    public string ToQueryString(
        (string Key, string? Value)? replace = null,
        (string Key, string Value)? toggle = null,
        int? page = null)
    {
        var parts = new List<string>();

        void Add(string key, string value) =>
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");

        var text = Text;
        var types = Types.ToList();
        var selections = Selections.ToDictionary(
            pair => pair.Key, pair => pair.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        var flags = new Dictionary<string, bool>(Flags, StringComparer.OrdinalIgnoreCase);
        var sort = Sort;
        var descending = Descending;
        var group = Group;

        if (toggle is { } switched)
        {
            if (string.Equals(switched.Key, "type", StringComparison.OrdinalIgnoreCase))
            {
                if (EntityTypes.TryParse(switched.Value, out var type) && !types.Remove(type)) types.Add(type);
            }
            else
            {
                var chosen = selections.TryGetValue(switched.Key, out var list) ? list : [];
                if (!chosen.Remove(switched.Value)) chosen.Add(switched.Value);
                selections[switched.Key] = chosen;
            }
        }

        if (replace is { } set)
        {
            switch (set.Key.ToLowerInvariant())
            {
                case "q": text = set.Value; break;
                case "sort": sort = set.Value ?? "name"; break;
                case "dir": descending = set.Value?.StartsWith('d') == true; break;
                case "group": group = set.Value; break;
                case "type":
                    types = set.Value is null ? [] : EntityTypes.TryParse(set.Value, out var replaced) ? [replaced] : [];
                    break;
                default:
                    if (flags.ContainsKey(set.Key) || set.Value is "0" or "1")
                    {
                        if (set.Value is null) flags.Remove(set.Key);
                        else flags[set.Key] = set.Value is not "0";
                    }
                    else if (set.Value is null)
                    {
                        selections.Remove(set.Key);
                    }
                    else
                    {
                        selections[set.Key] = [set.Value];
                    }

                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(text)) Add("q", text);
        foreach (var type in types) Add("type", EntityTypes.Slug(type));

        foreach (var (key, values) in selections.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var value in values) Add(key, value);
        }

        foreach (var (key, wanted) in flags.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Add(key, wanted ? "1" : "0");
        }

        foreach (var (key, range) in Ranges.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (range.Min is { } min) Add(key + "Min", Codex.Number(min));
            if (range.Max is { } max) Add(key + "Max", Codex.Number(max));
        }

        if (sort != "name") Add("sort", sort);
        if (descending) Add("dir", "desc");
        if (group is not null) Add("group", group);
        if (PageSize != DefaultPageSize) Add("size", PageSize.ToString(CultureInfo.InvariantCulture));

        var wantedPage = page ?? (replace is null && toggle is null ? Page : 1);
        if (wantedPage > 1) Add("page", wantedPage.ToString(CultureInfo.InvariantCulture));

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    public IEnumerable<(string Name, string Value)> Fields()
    {
        if (!string.IsNullOrWhiteSpace(Text)) yield return ("q", Text);

        foreach (var type in Types) yield return ("type", EntityTypes.Slug(type));

        foreach (var (key, values) in Selections.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            foreach (var value in values) yield return (key, value);
        }

        foreach (var (key, wanted) in Flags.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            yield return (key, wanted ? "1" : "0");
        }

        if (Sort != "name") yield return ("sort", Sort);
        if (Descending) yield return ("dir", "desc");
        if (Group is not null) yield return ("group", Group);
        if (PageSize != DefaultPageSize) yield return ("size", PageSize.ToString(CultureInfo.InvariantCulture));
    }

    public double? Bound(string measure, bool max) =>
        Ranges.TryGetValue(measure, out var range) ? max ? range.Max : range.Min : null;

    public bool Chosen(string facet, string value) =>
        Selections.TryGetValue(facet, out var values) &&
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    public bool ChosenType(EntityType type) => Types.Contains(type);

    public bool IsEmpty =>
        Text is null && Types.Count == 0 && Selections.All(s => s.Value.Count == 0) &&
        Flags.Count == 0 && Ranges.Count == 0;
}

public sealed record Listing(EntityDocument Shown, IReadOnlyList<Listing.Variant> Variants)
{
    public sealed record Variant(string Name, string? Icon, string Href, string? Colour);
}

public sealed class CodexSearch(EntityStore store, Codex codex, SearchIndex index, CodexView view)
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

public sealed record CodexResults(
    int Total,
    int Page,
    int Pages,
    IReadOnlyList<Listing> Rows,
    IReadOnlyList<FacetView> Facets,
    IReadOnlyDictionary<EntityType, int> Types,
    int AcrossTypes,
    IReadOnlyList<string> Measured);
