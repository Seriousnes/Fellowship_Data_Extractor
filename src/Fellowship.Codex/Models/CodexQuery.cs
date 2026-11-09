using System.Globalization;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record CodexQuery
{
    public const int DefaultPageSize = 50;

    public const int MaximumPageSize = 200;

    public string? Text { get; init; }

    public Category? Category { get; init; }

    public IReadOnlyList<EntityType> Types { get; init; } = [];

    public IReadOnlyDictionary<FacetType, IReadOnlyList<string>> Selections { get; init; } =
        new Dictionary<FacetType, IReadOnlyList<string>>();

    public IReadOnlyDictionary<FlagType, bool> Flags { get; init; } = new Dictionary<FlagType, bool>();

    public IReadOnlyList<Comparison> Comparisons { get; init; } = [];

    public SortColumn Sort { get; init; } = SortingType.Name;

    public bool Descending { get; init; }

    public FacetType? Group { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public static CodexQuery Parse(
        IQueryCollection query,
        ICodexService codex,
        IReadOnlySet<string> declared,
        Category? category,
        out string? error)
    {
        error = null;

        var selections = new Dictionary<FacetType, IReadOnlyList<string>>();
        var flags = new Dictionary<FlagType, bool>();
        var comparisons = new List<Comparison>();
        var measures = codex.Measures.Select(measure => measure.Key).ToArray();
        var types = new List<EntityType>();

        string? text = null, sort = null, group = null;
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
                    case BrowseParameter.Q:
                        text = raw.Count > 0 ? raw[^1] : null;
                        break;

                    case BrowseParameter.Type:
                        foreach (var slug in values)
                        {
                            if (!EntityTypes.TryParse(slug, out var named) || !PublicTypes.IsListed(named))
                            {
                                error = PublicTypes.NoSuchType(slug);
                                return new CodexQuery { Category = category };
                            }

                            if (category is not null && PublicTypes.Category(named) != category)
                            {
                                error = PublicTypes.NotInCategory(slug, category);
                                return new CodexQuery { Category = category };
                            }

                            types.Add(named);
                        }

                        break;

                    case BrowseParameter.Sort:
                        if (values.Length > 0) sort = values[^1];
                        break;

                    case BrowseParameter.Dir:
                        descending = values.Length > 0 &&
                            values[^1].StartsWith('d');
                        break;

                    case BrowseParameter.Group:
                        group = values.Length > 0 ? values[^1] : null;
                        break;

                    case BrowseParameter.Page:
                        if (!TryNumber(values, out var wanted, out error)) return new CodexQuery { Category = category };
                        page = wanted is > 0 ? (int)wanted : 1;
                        break;

                    case BrowseParameter.Size:
                        if (!TryNumber(values, out var size, out error)) return new CodexQuery { Category = category };
                        pageSize = size is > 0 ? Math.Min((int)size, MaximumPageSize) : DefaultPageSize;
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"'{control}' is declared as a filter parameter and nothing reads it, so a caller " +
                            "would find it in the published document and see it do nothing.");
                }

                continue;
            }

            if (QueryKeys.TryParse<FacetType>(key, out var faceted))
            {
                var facet = codex.Facet(faceted);
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
                            return new CodexQuery { Category = category };
                        }

                        chosen.Add(value);
                    }
                }

                selections[faceted] = chosen;
                continue;
            }

            if (QueryKeys.TryParse<FlagType>(key, out var flag))
            {
                var value = values.Length == 0 || !(values[^1] is "0" or "false" or "no");
                flags[flag] = value;
                continue;
            }

            if (Models.Comparisons.TryParse(key, measures, out var measure, out var comparator))
            {
                foreach (var entry in values)
                {
                    if (!TryNumber([entry], out var bound, out error)) return new CodexQuery { Category = category };

                    comparisons.Add(new Comparison(measure, comparator, bound!.Value));
                }

                continue;
            }

            error = $"Unknown parameter '{key}'. The filters are " +
                string.Join(", ", codex.Facets.Select(f => f.Key)
                    .Concat(codex.Flags.Select(f => f.Key))
                    .Concat(Models.Comparisons.Parameters(measures))
                    .Concat(declared)) + ".";
            return new CodexQuery { Category = category };
        }

        var searched = string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        sort ??= DefaultSort(searched).Key;

        if (!SortColumn.TryParse(sort, codex, out var sorted))
        {
            error = $"'{sort}' is not a column. Sort by one of " +
                string.Join(", ", codex.Sortings.Select(s => s.Key).Concat(measures)) + ".";
            return new CodexQuery { Category = category };
        }

        FacetType? grouped = null;

        if (group is not null)
        {
            if (!QueryKeys.TryParse<FacetType>(group, out var named))
            {
                error = $"'{group}' is not a facet to group by. The facets are " +
                    string.Join(", ", codex.Facets.Select(f => f.Key)) + ".";
                return new CodexQuery { Category = category };
            }

            grouped = named;
        }

        return new CodexQuery
        {
            Text = searched,
            Category = category,
            Types = types,
            Selections = selections,
            Flags = flags,
            Comparisons = comparisons,
            Sort = sorted,
            Descending = descending,
            Group = grouped,
            Page = page,
            PageSize = pageSize,
        };
    }

    private static BrowseParameter? Declared(IReadOnlySet<string> declared, string key) =>
        !declared.Contains(key) ? null
        : QueryKeys.TryParse<BrowseParameter>(key, out var control) ? control
        : throw new InvalidOperationException(
            $"'{key}' is declared as a filter parameter and no {nameof(BrowseParameter)} names it, so a caller " +
            "would find it in the published document and see it do nothing.");

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

    public CodexQuery WithType(EntityType? type) => this with { Types = type is { } only ? [only] : [], Page = 1 };

    public CodexQuery Toggled(EntityType type) =>
        this with { Types = Types.Contains(type) ? [.. Types.Where(kept => kept != type)] : [.. Types, type], Page = 1 };

    public CodexQuery Toggled(FacetType facet, string value)
    {
        var chosen = Selections.GetValueOrDefault(facet)?.ToList() ?? [];
        if (!chosen.Remove(value)) chosen.Add(value);

        return this with { Selections = new Dictionary<FacetType, IReadOnlyList<string>>(Selections) { [facet] = chosen }, Page = 1 };
    }

    public CodexQuery WithFacet(FacetType facet, string? value)
    {
        var selections = new Dictionary<FacetType, IReadOnlyList<string>>(Selections);

        if (value is null) selections.Remove(facet);
        else selections[facet] = [value];

        return this with { Selections = selections, Page = 1 };
    }

    public CodexQuery WithFlag(FlagType flag, bool? wanted)
    {
        var flags = new Dictionary<FlagType, bool>(Flags);

        if (wanted is { } stated) flags[flag] = stated;
        else flags.Remove(flag);

        return this with { Flags = flags, Page = 1 };
    }

    public CodexQuery Without(Comparison comparison)
    {
        var kept = Comparisons.ToList();
        kept.Remove(comparison);

        return this with { Comparisons = kept, Page = 1 };
    }

    /// <summary>This query with no type and no choice of <paramref name="facet"/>, the part of it a page that fixes them leaves to the reader.</summary>
    public CodexQuery Unpinned(FacetType facet) =>
        this with { Types = [], Selections = Selections.Where(pair => pair.Key != facet).ToDictionary() };

    /// <summary>This query narrowed to <paramref name="type"/> and to <paramref name="value"/> of <paramref name="facet"/>, keeping its page.</summary>
    public CodexQuery Pinned(EntityType type, FacetType facet, string value) =>
        this with
        {
            Types = [type],
            Selections = new Dictionary<FacetType, IReadOnlyList<string>>(Selections) { [facet] = [value] },
        };

    public CodexQuery WithSort(SortColumn sort, bool descending) => this with { Sort = sort, Descending = descending, Page = 1 };

    public CodexQuery WithGroup(FacetType? group) => this with { Group = group, Page = 1 };

    public string ToQueryString(int? page = null)
    {
        var parts = Parameters(comparisons: true).Select(field => $"{Uri.EscapeDataString(field.Name)}={Uri.EscapeDataString(field.Value)}").ToList();

        var wanted = page ?? Page;
        if (wanted > 1) parts.Add($"{QueryKeys.Key(BrowseParameter.Page)}={wanted.ToString(CultureInfo.InvariantCulture)}");

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    /// <summary>Every parameter except the comparisons. The comparison form sends its own comparisons.</summary>
    public IEnumerable<(string Name, string Value)> Fields() => Parameters(comparisons: false);

    private IEnumerable<(string Name, string Value)> Parameters(bool comparisons)
    {
        if (!string.IsNullOrWhiteSpace(Text)) yield return (QueryKeys.Key(BrowseParameter.Q), Text);

        foreach (var type in Types) yield return (QueryKeys.Key(BrowseParameter.Type), EntityTypes.Slug(type));

        foreach (var (key, values) in Selections.OrderBy(pair => QueryKeys.Key(pair.Key), StringComparer.Ordinal))
        {
            foreach (var value in values) yield return (QueryKeys.Key(key), value);
        }

        foreach (var (key, wanted) in Flags.OrderBy(pair => QueryKeys.Key(pair.Key), StringComparer.Ordinal))
        {
            yield return (QueryKeys.Key(key), wanted ? "1" : "0");
        }

        foreach (var comparison in Comparisons.Where(_ => comparisons))
        {
            yield return (comparison.Parameter, FellowshipCodex.Number(comparison.Value));
        }

        if (Sort != DefaultSort(Text)) yield return (QueryKeys.Key(BrowseParameter.Sort), Sort.Key);
        if (Descending) yield return (QueryKeys.Key(BrowseParameter.Dir), "desc");
        if (Group is { } group) yield return (QueryKeys.Key(BrowseParameter.Group), QueryKeys.Key(group));
        if (PageSize != DefaultPageSize) yield return (QueryKeys.Key(BrowseParameter.Size), PageSize.ToString(CultureInfo.InvariantCulture));
    }

    public static SortColumn DefaultSort(string? text) => text is null ? SortingType.Name : SortingType.Relevance;

    public bool Chosen(FacetType facet, string value) =>
        Selections.TryGetValue(facet, out var values) &&
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    public bool ChosenType(EntityType type) => Types.Contains(type);

    public bool IsEmpty =>
        Text is null && Types.Count == 0 && Selections.All(s => s.Value.Count == 0) &&
        Flags.Count == 0 && Comparisons.Count == 0;
}

public sealed record CodexResults(
    int Total,
    int Page,
    int Pages,
    IReadOnlyList<EntityDocument> Rows,
    IReadOnlyList<FacetCount> Facets,
    IReadOnlyDictionary<EntityType, int> Types,
    EntityType? Chosen,
    IReadOnlyList<string> Measured,
    IReadOnlyList<FlagType> Flagged);

