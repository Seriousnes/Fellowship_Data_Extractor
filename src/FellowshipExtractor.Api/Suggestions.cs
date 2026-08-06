using Fellowship.SDK.Documents;

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
    /// rather than compared: a slug naming no <see cref="EntityType"/> narrows to nothing at all
    /// rather than being dropped, because dropping it would answer an unresolved type with the whole
    /// corpus and read as though the name had been understood.
    /// </summary>
    public SearchResponse For(string? text, string? type, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchResponse.Empty;

        var hits = index.Search(text, Ceiling);

        // Every hit names a real EntityType, because the index holds nothing else, which is what
        // makes the slug here always a real one.
        var groups = hits
            .GroupBy(hit => EntityTypes.TypeOf(hit.Document))
            .Select(group => new SearchGroup(EntityTypes.Slug(group.Key), PublicTypes.Label(group.Key), group.Count()))
            .OrderByDescending(group => group.Count)
            .ToArray();

        IReadOnlyList<SearchIndex.Hit> chosen = hits;

        if (!string.IsNullOrWhiteSpace(type))
        {
            chosen = EntityTypes.TryParse(type, out var wanted)
                ? [.. hits.Where(hit => EntityTypes.TypeOf(hit.Document) == wanted)]
                : [];
        }

        return new SearchResponse(
            text,
            hits.Count,
            groups,
            [.. chosen.Take(limit).Select(hit => SearchResult.From(hit.Document, view))]);
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
/// row's display name, or its noun and id where it has none. The name fallback is the ordinary case
/// rather than a corner: measured over the export, 5,997 of the 12,437 documents carry no display
/// name.
///
/// Slot, armour type and the hero list only ever come from an <see cref="Item"/>; every other
/// document type answers null for all three, which is simply the fact that only <see cref="Item"/>
/// states any of them about itself.
/// </summary>
public sealed record SearchResult(
    string Type,
    string TypeLabel,
    int Id,
    string Name,
    string IconUrl,
    string Href,
    string Tooltip,
    string? Slot,
    string? ArmorType,
    IReadOnlyList<string>? Heroes)
{
    public static SearchResult From(EntityDocument document, CodexView view)
    {
        var type = EntityTypes.TypeOf(document);
        var item = document as Item;

        return new(
            EntityTypes.Slug(type),
            PublicTypes.Label(type),
            document.Id,
            PublicTypes.NameOf(document),
            view.Icon(DocumentFields.Text(document, nameof(Item.Icon))),
            view.Href(document),
            view.Tip(document),
            item?.Slot,
            item?.ArmorType?.ToString(),
            item?.Heroes?.Select(hero => hero.ToString()).ToArray());
    }
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
