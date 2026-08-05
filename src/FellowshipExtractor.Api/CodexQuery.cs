using System.Globalization;

namespace FellowshipExtractor.Api;

/// <summary>
/// A filtered reading of the corpus, as a caller spells it in a query string.
///
/// Every narrowing is a facet, a flag or a measure the <see cref="Codex"/> defines, so this parses
/// against that list rather than naming fields itself: a facet added there is filterable here
/// without this type being touched.
///
/// Both halves of a narrowing are checked. A parameter that names nothing is an error rather than a
/// silent no-op, for the same reason the tooltip query rejects an unknown stat - a reader who
/// mistypes <c>slots=Head</c> would otherwise be shown the whole corpus and told nothing - and so is
/// a value no row carries, which is what let <c>?hero=Lisa</c> and <c>?rarity=Champion</c> answer
/// 200 with an empty page and a chip spelling the internal word back. <c>type</c> is checked against
/// the slugs of the public types, refusing everything else and a shipped table name in particular,
/// and every other value against the list <see cref="Codex.Offered"/> counts off the corpus.
/// </summary>
public sealed record CodexQuery
{
    public const int DefaultPageSize = 50;
    private const int MaximumPageSize = 200;

    /// <summary>The words a reader typed, matched against names the same way the omnibox matches.</summary>
    public string? Text { get; init; }

    /// <summary>
    /// The tables to read, or every public table when the caller named none.
    ///
    /// Held as the shipped table name because that is what a row carries and what a filter compares
    /// against, and turned back into a slug on the way out. A slug is the only spelling that crosses
    /// the wire in either direction: it is what <see cref="Parse"/> accepts and what
    /// <see cref="ToQueryString"/> and <see cref="Fields"/> write.
    /// </summary>
    public IReadOnlyList<string> Tables { get; init; } = [];

    /// <summary>What the caller chose under each facet, keyed by the facet's own key.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Selections { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The flags the caller asked for, each either required or forbidden.</summary>
    public IReadOnlyDictionary<string, bool> Flags { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, (double? Min, double? Max)> Ranges { get; init; } =
        new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The measure or column the results are ordered by. Name when the caller named none.</summary>
    public string Sort { get; init; } = "name";

    public bool Descending { get; init; }

    /// <summary>The facet the results table breaks into headed sections under, where one was asked for.</summary>
    public string? Group { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public static CodexQuery Parse(IQueryCollection query, Codex codex, out string? error)
    {
        error = null;

        var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var ranges = new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);
        var tables = new List<string>();

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

            switch (key.ToLowerInvariant())
            {
                case "q":
                    text = raw.Count > 0 ? raw[^1] : null;
                    continue;

                case "type":
                    // A slug, and nothing else. The shipped table name is deliberately not a
                    // spelling this understands: accepting it would keep it working as an address,
                    // and a link carrying one is then something a reader can be handed and repeat.
                    foreach (var slug in values)
                    {
                        if (PublicTypes.TableOf(slug) is not { } named)
                        {
                            error = $"'{slug}' is not a type. The types are " +
                                string.Join(", ", PublicTypes.All.Select(t => t.Slug)) + ".";
                            return new CodexQuery();
                        }

                        tables.Add(named);
                    }

                    continue;

                case "sort":
                    if (values.Length > 0) sort = values[^1];
                    continue;

                case "dir":
                    descending = values.Length > 0 &&
                        values[^1].StartsWith('d');
                    continue;

                case "group":
                    group = values.Length > 0 ? values[^1] : null;
                    continue;

                case "page":
                    if (!TryNumber(values, out var wanted, out error)) return new CodexQuery();
                    page = wanted is > 0 ? (int)wanted : 1;
                    continue;

                case "size":
                    if (!TryNumber(values, out var size, out error)) return new CodexQuery();
                    pageSize = size is > 0 ? Math.Min((int)size, MaximumPageSize) : DefaultPageSize;
                    continue;
            }

            if (codex.Facet(key) is { } facet)
            {
                // A value no row carries is an error rather than a filter that leaves nothing, on the
                // same terms as a parameter that names nothing. ?hero=Lisa matched no row already,
                // because the export names heroes the way the game prints them, but it answered 200
                // with an empty page whose chip read "Hero: Lisa" and whose every link carried the
                // codename on: a spelling the build files and never shows was a public address a
                // reader could bookmark and hand to somebody. Checked against the values the panel
                // offers, so an internal spelling is turned away by the sentence a typo is.
                //
                // Refused outright rather than per value, so a two-value parameter cannot half apply
                // and leave a narrowing on screen that the address does not describe.
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
                // Present with no value is "required", which is what a checkbox posts. An explicit
                // 0, false or no is the other half of the tri-state: rows that do not carry it.
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
                    .Concat(["q", "type", "sort", "dir", "group", "page", "size"])) + ".";
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
            Tables = tables,
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

    /// <summary>Splits cooldownMin into the measure it bounds and which end it is.</summary>
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

    /// <summary>The query as a query string, for the links a results page writes back to itself.</summary>
    public string ToQueryString(
        (string Key, string? Value)? replace = null,
        (string Key, string Value)? toggle = null,
        int? page = null)
    {
        var parts = new List<string>();

        void Add(string key, string value) =>
            parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");

        var text = Text;
        var tables = Tables.ToList();
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
                if (!tables.Remove(switched.Value)) tables.Add(switched.Value);
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
                    tables = set.Value is null ? [] : [set.Value];
                    break;
                default:
                    // A flag lives in its own map, so replacing one has to reach that map: reading
                    // the key as a facet would leave the flag in force and write it out again.
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
        foreach (var table in tables)
        {
            if (PublicTypes.SlugOf(table) is { } slug) Add("type", slug);
        }

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

    /// <summary>
    /// The query as form fields, so a GET form can carry everything already in force and change only
    /// what it is for. The ranges are left out: the form that needs this is the ranges form, and its
    /// own inputs are the current values.
    /// </summary>
    public IEnumerable<(string Name, string Value)> Fields()
    {
        if (!string.IsNullOrWhiteSpace(Text)) yield return ("q", Text);

        // The same slug the links carry. A hidden field is submitted straight back to Parse, so a
        // shipped table name here would answer "that filter could not be read" the moment a reader
        // applied a range from a type tab.
        foreach (var table in Tables)
        {
            if (PublicTypes.SlugOf(table) is { } slug) yield return ("type", slug);
        }

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

    /// <summary>One end of a measure's range as the caller set it, or nothing.</summary>
    public double? Bound(string measure, bool max) =>
        Ranges.TryGetValue(measure, out var range) ? max ? range.Max : range.Min : null;

    public bool Chosen(string facet, string value) =>
        Selections.TryGetValue(facet, out var values) &&
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    public bool ChosenType(string table) => Tables.Contains(table, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether anything at all narrows the corpus, which is what an empty state reads.</summary>
    public bool IsEmpty =>
        Text is null && Tables.Count == 0 && Selections.All(s => s.Value.Count == 0) &&
        Flags.Count == 0 && Ranges.Count == 0;
}

/// <summary>
/// Runs a <see cref="CodexQuery"/> against the public corpus: filter, then rank, then page. Filtering
/// and facet counting both read the <see cref="Codex"/>'s selectors, so the counts beside a filter
/// always describe the rows that filter would leave.
/// </summary>
public sealed class CodexSearch(EntityStore store, Codex codex, SearchIndex index)
{
    public CodexResults Run(CodexQuery query)
    {
        // A text query narrows first, because it is the one filter with a relevance order of its
        // own: the rank the index assigns is what "sort by name" means for a search, and recomputing
        // it after the other filters would cost the same and say the same thing.
        IEnumerable<Entity> read = query.Text is { } text
            ? index.Search(text, int.MaxValue).Select(hit => hit.Entity)
            : store.Entities;

        // The corpus a reader may meet, which is not the whole export. 11 of its 18 tables carry no
        // row the game ever names, so a row of one of them has nothing to be called but its gameplay
        // tag, and is not a result, a facet count or a tab.
        //
        // Narrowed once, here, rather than tested inside the filter. Every tally the page draws (the
        // facet counts, the type tabs, the range boxes) is taken off this same array, and a boundary
        // applied to some of them and not the others is exactly how a hidden row reappears in a
        // number after being kept off the list.
        var rows = read.Where(row => PublicTypes.IsPublic(row.Table)).ToArray();

        var matched = rows.Where(row => Matches(row, query)).ToArray();

        var ordered = Order(matched, query);

        var pages = Math.Max(1, (ordered.Length + query.PageSize - 1) / query.PageSize);
        var page = Math.Min(query.Page, pages);
        var window = ordered.Skip((page - 1) * query.PageSize).Take(query.PageSize).ToArray();

        // The facet panel counts the rows every *other* filter left, so choosing a second slot is
        // still offered after the first one is chosen. Counting the rows the panel's own filter left
        // would leave every unchosen value at zero and the panel unusable after one click.
        var facets = codex.Facets
            .Select(facet => codex.View(facet, Without(rows, query, facet.Key)))
            .Where(view => view.Values.Count > 0)
            .ToArray();

        // The type tabs count every type the *rest* of the filter leaves, not the chosen one only.
        // A reader on the Items tab of a search wants to see that there are also three abilities by
        // that name; counting under the type filter would show zero beside every other tab.
        var across = rows.Where(row => Matches(row, query, skipTables: true)).ToArray();

        var types = across
            .GroupBy(row => row.Table!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        // Which measures are worth offering a range box for, counted the same way a facet is: over
        // the rows every filter but the ranges leaves. Counting over the page instead would make a
        // box vanish the moment its own range narrowed the page past the rows that carry it.
        var unbounded = rows.Where(row => Matches(row, query with { Ranges = NoRanges })).ToArray();

        var measured = codex.Measures
            .Where(measure => unbounded.Any(row => measure.Value(row) is not null))
            .Select(measure => measure.Key)
            .ToArray();

        return new CodexResults(ordered.Length, page, pages, window, facets, types, across.Length, measured);
    }

    private static readonly IReadOnlyDictionary<string, (double?, double?)> NoRanges =
        new Dictionary<string, (double?, double?)>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The rows every filter but one leaves, which is what that one filter's counts are of.</summary>
    private Entity[] Without(IReadOnlyList<Entity> rows, CodexQuery query, string facet) =>
        [.. rows.Where(row => Matches(row, query, skipFacet: facet))];

    private bool Matches(Entity row, CodexQuery query, string? skipFacet = null, bool skipTables = false)
    {
        if (!skipTables && query.Tables.Count > 0 &&
            !query.Tables.Contains(row.Table!, StringComparer.OrdinalIgnoreCase))
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

            // A row with no value for the measure is not in a range at all. A cooldown filter is a
            // question about abilities that have one, so an item is out rather than counted as zero.
            if (measure.Value(row) is not { } value) return false;
            if (range.Min is { } min && value < min) return false;
            if (range.Max is { } max && value > max) return false;
        }

        return true;
    }

    private Entity[] Order(Entity[] rows, CodexQuery query)
    {
        // A grouped table has to arrive grouped, because the page draws a heading whenever the value
        // changes and rows in sort order would head every second row.
        if (query.Group is not null && codex.Facet(query.Group) is { } grouping)
        {
            return
            [
                .. Order(rows, query with { Group = null })
                    .OrderBy(row => grouping.Values(row).FirstOrDefault() ?? "￿", StringComparer.OrdinalIgnoreCase),
            ];
        }

        // A text search already arrives in relevance order and the default sort keeps it. Re-sorting
        // by name would throw away the ranking that put the exact match first.
        if (query.Text is not null && query.Sort == "name" && !query.Descending) return rows;

        IOrderedEnumerable<Entity> ordered = query.Sort switch
        {
            // By the name the row prints, which for more than half of them is "Effect 2018" rather
            // than a display name. Ordering by the gameplay tag underneath instead would sort the
            // page by something none of it shows, and read as no order at all.
            "name" => query.Descending
                ? rows.OrderByDescending(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase),

            // By the heading the type column draws, for the same reason: the shipped table names run
            // CRAICharacter before GameplayAbility before ItemID, which beside Creatures, Ability and
            // Items on screen is an alphabet the reader cannot see.
            "type" => query.Descending
                ? rows.OrderByDescending(row => PublicTypes.Label(row.Table), StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => PublicTypes.Label(row.Table), StringComparer.OrdinalIgnoreCase),

            _ => Measured(rows, codex.Measure(query.Sort)!, query.Descending),
        };

        return
        [
            .. ordered
                .ThenBy(row => PublicTypes.NameOf(row), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.NetId),
        ];
    }

    /// <summary>
    /// Ordering by a number, with the rows that carry none last in both directions. A descending
    /// cooldown sort exists to see the longest cooldowns, and putting every item with no cooldown at
    /// the top would answer with a page of them.
    /// </summary>
    private static IOrderedEnumerable<Entity> Measured(Entity[] rows, Measure measure, bool descending) =>
        descending
            ? rows.OrderBy(row => measure.Value(row) is null).ThenByDescending(row => measure.Value(row))
            : rows.OrderBy(row => measure.Value(row) is null).ThenBy(row => measure.Value(row));
}

/// <summary>What a filtered reading answered with, and everything a results page draws from it.</summary>
public sealed record CodexResults(
    int Total,
    int Page,
    int Pages,
    IReadOnlyList<Entity> Rows,
    IReadOnlyList<FacetView> Facets,
    IReadOnlyDictionary<string, int> Types,
    int AcrossTypes,
    IReadOnlyList<string> Measured);
