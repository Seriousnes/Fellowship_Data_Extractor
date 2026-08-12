using System.Globalization;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record CodexQuery
{
    public const int DefaultPageSize = 50;

    public const int MaximumPageSize = 200;

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

    public static CodexQuery Parse(
        IQueryCollection query, ICodexService codex, IReadOnlySet<string> declared, out string? error)
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

            if (Declared(declared, key) is { } control)
            {
                switch (control)
                {
                    case "q":
                        text = raw.Count > 0 ? raw[^1] : null;
                        break;

                    case "type":
                        foreach (var slug in values)
                        {
                            if (!EntityTypes.TryParse(slug, out var named) || !PublicTypes.Listed(named))
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
                var chosen = new List<string>();

                foreach (var entry in raw)
                {
                    var whole = (entry ?? string.Empty).Trim();
                    if (whole.Length == 0) continue;

                    if (offered.Contains(whole, StringComparer.OrdinalIgnoreCase))
                    {
                        chosen.Add(whole);
                        continue;
                    }

                    foreach (var part in whole.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var value = part.Trim();
                        if (value.Length == 0) continue;

                        if (!offered.Contains(value, StringComparer.OrdinalIgnoreCase))
                        {
                            error = $"'{value}' is not a value of {facet.Label}. The values are " +
                                string.Join(", ", offered) + ".";
                            return new CodexQuery();
                        }

                        chosen.Add(value);
                    }
                }

                selections[facet.Key] = chosen;
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
                    .Concat(declared)) + ".";
            return new CodexQuery();
        }

        if (codex.Sorting(sort) is null && codex.Measure(sort) is null)
        {
            error = $"'{sort}' is not a column. Sort by one of " +
                string.Join(", ", codex.Sortings.Select(s => s.Key).Concat(codex.Measures.Select(m => m.Key))) + ".";
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

    private static string? Declared(IReadOnlySet<string> declared, string key) =>
        declared.Contains(key) ? key.ToLowerInvariant() : null;

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
            if (range.Min is { } min) Add(key + "Min", FellowshipCodex.Number(min));
            if (range.Max is { } max) Add(key + "Max", FellowshipCodex.Number(max));
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

    public const string NamedFlag = "named";

    public bool StatesNamed => Flags.ContainsKey(NamedFlag);

    public bool Named => !Flags.TryGetValue(NamedFlag, out var wanted) || wanted;

    public bool IsEmpty =>
        Text is null && Types.Count == 0 && Selections.All(s => s.Value.Count == 0) &&
        Flags.Count == 0 && Ranges.Count == 0;
}

public sealed record CodexResults(
    int Total,
    int Page,
    int Pages,
    IReadOnlyList<EntityDocument> Rows,
    IReadOnlyList<FacetView> Facets,
    IReadOnlyDictionary<EntityType, int> Types,
    EntityType Chosen,
    IReadOnlyList<string> Measured);
