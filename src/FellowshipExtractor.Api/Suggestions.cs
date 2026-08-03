using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// What the omnibox draws under itself: the rows a query reaches, one page of them, and a tab per
/// type with the count of hits that type holds.
///
/// The tabs are counted over every hit and the rows are the narrowed ones, so a reader on the Items
/// tab of a search can still see that three abilities answer to the same name.
/// </summary>
public sealed class Suggestions(SearchIndex index, CodexView view)
{
    public const int Ceiling = 2000;

    /// <summary>
    /// One query answered. The type is a slug, the spelling the tabs post back, and it is resolved
    /// rather than compared: a slug naming nothing public narrows to nothing at all rather than being
    /// dropped, because dropping it would answer an internal table name with the whole corpus and
    /// read as though the name had been understood.
    /// </summary>
    public SearchResponse For(string? text, string? type, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchResponse.Empty;

        var hits = index.Search(text, Ceiling);

        // Every hit is of a public type, because the index holds nothing else, which is what makes
        // the slug here always a real one.
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

/// <summary>
/// One row as a reader meets it: what it is called, what kind of thing it is, and where to read it.
///
/// Named and typed the way a reader is shown and not the way the build spells it. The name is the
/// row's display name, or its noun and id where it has none, and never its gameplay tag; the type is
/// the route slug and the collection heading, and never the tag table the row lives in. The name
/// fallback is the ordinary case rather than a corner, because 7,957 of the 14,478 public rows carry
/// no display name.
///
/// A row of no public type answers with an empty slug and the fallback heading rather than with the
/// table's own spelling, so nothing reachable from here can leak one. Handing this such a row is
/// still a bug in whatever selected it.
/// </summary>
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

/// <summary>
/// One type tab over a set of results: the slug it posts back as <c>type=</c>, the heading it is
/// drawn under, and how many of the hits are of it.
/// </summary>
public sealed record SearchGroup(string Type, string Label, int Count);

public sealed record SearchResponse(
    string Query, int Total, IReadOnlyList<SearchGroup> Groups, IReadOnlyList<SearchResult> Results)
{
    public static readonly SearchResponse Empty = new(string.Empty, 0, [], []);
}

public sealed record EntitiesResponse(int Total, int Page, int Pages, IReadOnlyList<SearchResult> Rows);
