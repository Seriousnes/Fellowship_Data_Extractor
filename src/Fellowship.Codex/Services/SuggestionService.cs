using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ISuggestionService
{
    SearchResponse For(string? text, string? type, int limit);
}
public sealed class SuggestionService(ISearchIndexService index, ICodexViewService view) : ISuggestionService
{
    public const int Ceiling = 2000;

    public const int DefaultLimit = 10;

    public const int MaximumLimit = 100;

    public static int Limit(string? value) =>
        int.TryParse(value, out var wanted) ? Math.Clamp(wanted, 1, MaximumLimit) : DefaultLimit;

    public SearchResponse For(string? text, string? type, int limit)
    {
        if (string.IsNullOrWhiteSpace(text)) return SearchResponse.Empty;

        var hits = index.Search(text, Ceiling)
            .Where(hit => view.Codex.Flags.All(flag => flag.Admits(hit.Document)))
            .GroupBy(hit => (EntityTypes.TypeOf(hit.Document), hit.Document.Id))
            .Select(readings => readings.First())
            .ToArray();

        var groups = hits
            .GroupBy(hit => EntityTypes.TypeOf(hit.Document))
            .OrderBy(group => PublicTypes.Rank(group.Key))
            .Select(group => new SearchGroup(EntityTypes.Slug(group.Key), PublicTypes.Label(group.Key), group.Count()))
            .ToArray();

        IReadOnlyList<SearchIndexService.Hit> chosen = hits;

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
            [
                .. chosen
                    .Take(limit)
                    .OrderBy(hit => PublicTypes.Rank(EntityTypes.TypeOf(hit.Document)))
                    .Select(hit => SearchResults.From(hit.Document, view)),
            ]);
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
