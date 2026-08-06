using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed class Suggestions(SearchIndex index, CodexView view)
{
    public const int Ceiling = 2000;

    public const int DefaultLimit = 10;

    public const int MaximumLimit = 100;

    public static readonly IReadOnlyList<ApiParameter> Parameters =
    [
        new("q", ApiValue.Text, "The text to match. An empty one answers no results."),
        new("type", ApiValue.Text, "One entity type slug, to narrow the results to it."),
        new("limit", ApiValue.Whole,
            $"How many results to answer, 1 to {MaximumLimit}. Defaults to {DefaultLimit}."),
    ];

    public static int Limit(string? asked) =>
        int.TryParse(asked, out var wanted) ? Math.Clamp(wanted, 1, MaximumLimit) : DefaultLimit;

    public SearchResponse For(string? text, string? type, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchResponse.Empty;

        var hits = index.Search(text, Ceiling)
            .GroupBy(hit => (EntityTypes.TypeOf(hit.Document), hit.Document.Id))
            .Select(readings => readings.First())
            .ToArray();

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
            hits.Length,
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
        var item = document as ItemDocument;

        return new(
            EntityTypes.Slug(type),
            PublicTypes.Label(type),
            document.Id,
            PublicTypes.NameOf(document),
            view.Icon(DocumentFields.Text(document, nameof(ItemDocument.Icon))),
            view.Href(document),
            view.Tip(document),
            item?.Slot,
            item?.ArmorType?.ToString(),
            item?.Heroes?.Select(hero => hero.ToString()).ToArray());
    }
}

public sealed record SearchGroup(string Type, string Label, int Count);

public sealed record SearchResponse(
    string Query, int Total, IReadOnlyList<SearchGroup> Groups, IReadOnlyList<SearchResult> Results)
{
    public static readonly SearchResponse Empty = new(string.Empty, 0, [], []);
}

public sealed record EntitiesResponse(int Total, int Page, int Pages, IReadOnlyList<SearchResult> Rows);
