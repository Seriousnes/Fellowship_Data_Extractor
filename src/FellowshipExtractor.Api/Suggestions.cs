using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed class Suggestions(SearchIndex index, CodexView view)
{
    public const int Ceiling = 2000;

    public SearchResponse For(string? text, string? type, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchResponse.Empty;

        var hits = index.Search(text, Ceiling);

        var groups = hits
            .GroupBy(hit => hit.Entity.Table!, StringComparer.Ordinal)
            .Select(group => new SearchGroup(
                PublicTypes.SlugOf(group.Key)!, PublicTypes.Label(group.Key), group.Count()))
            .OrderByDescending(group => group.Count)
            .ToArray();

        IReadOnlyList<SearchIndex.Hit> chosen = hits;

        if (!string.IsNullOrWhiteSpace(type))
        {
            chosen = PublicTypes.TableOf(type) is { } table
                ? [.. hits.Where(hit => string.Equals(hit.Entity.Table, table, StringComparison.Ordinal))]
                : [];
        }

        return new SearchResponse(
            text,
            hits.Count,
            groups,
            [.. chosen.Take(limit).Select(hit => SearchResult.From(hit.Entity, view))]);
    }

    public static IReadOnlyList<string> Words(string? text)
    {
        if (text is null) return [];

        var words = new List<string>();
        var at = 0;

        while (at < text.Length)
        {
            while (at < text.Length && !char.IsLetterOrDigit(text[at])) at++;

            var start = at;
            while (at < text.Length && char.IsLetterOrDigit(text[at])) at++;

            if (at > start) words.Add(text[start..at].ToLowerInvariant());
        }

        return words;
    }

    public static IReadOnlyList<Run> Mark(string name, IReadOnlyList<string> words)
    {
        var runs = new List<Run>();
        var at = 0;

        while (at < name.Length)
        {
            var start = at;
            var word = char.IsLetterOrDigit(name[at]);
            while (at < name.Length && char.IsLetterOrDigit(name[at]) == word) at++;

            var part = name[start..at];
            var hit = word
                ? words.FirstOrDefault(typed => part.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                : null;

            if (hit is null)
            {
                runs.Add(new Run(part, false));
                continue;
            }

            runs.Add(new Run(part[..hit.Length], true));
            if (part.Length > hit.Length) runs.Add(new Run(part[hit.Length..], false));
        }

        return runs;
    }

    public readonly record struct Run(string Text, bool Hit);
}

public sealed record SearchResult(
    string Type,
    string TypeLabel,
    int NetId,
    string Name,
    string IconUrl,
    string Href,
    string Tooltip,
    string? Slot,
    string? ArmorType,
    IReadOnlyList<string>? Heroes)
{
    public static SearchResult From(Entity entity, CodexView view) => new(
        PublicTypes.SlugOf(entity.Table) ?? string.Empty,
        PublicTypes.Label(entity.Table),
        entity.NetId ?? 0,
        PublicTypes.NameOf(entity),
        view.Icon(entity.Icon),
        view.Href(entity),
        view.Tip(entity),
        entity.Slot,
        entity.ArmorType,
        entity.Heroes);
}

public sealed record SearchGroup(string Type, string Label, int Count);

public sealed record SearchResponse(
    string Query, int Total, IReadOnlyList<SearchGroup> Groups, IReadOnlyList<SearchResult> Results)
{
    public static readonly SearchResponse Empty = new(string.Empty, 0, [], []);
}

public sealed record EntitiesResponse(int Total, int Page, int Pages, IReadOnlyList<SearchResult> Rows);
