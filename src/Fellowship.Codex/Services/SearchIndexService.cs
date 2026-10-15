using System.Globalization;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ISearchIndexService
{
    IReadOnlyList<SearchIndexService.Hit> Search(string text, int limit);
}
public sealed class SearchIndexService : ISearchIndexService
{
    private readonly EntityDocument[] docs;

    private readonly string[] display;

    private readonly string[][] displayWords;

    private readonly string[] terms;

    private readonly int[][] postings;

    private readonly Dictionary<int, int[]> carrying;

    public SearchIndexService(IEntityStoreService store, ILogger<SearchIndexService> logger)
    {
        docs = [.. store.Entities.Where(document => PublicTypes.IsListed(EntityTypes.TypeOf(document)))];

        display = new string[docs.Length];
        displayWords = new string[docs.Length][];

        var building = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var reachable = 0;

        for (var at = 0; at < docs.Length; at++)
        {
            var name = PublicTypes.DisplayNameOf(docs[at]) ?? string.Empty;

            display[at] = name.ToLowerInvariant();
            displayWords[at] = [.. Words(name).Distinct(StringComparer.Ordinal)];

            if (displayWords[at].Length > 0) reachable++;

            foreach (var term in displayWords[at])
            {
                if (!building.TryGetValue(term, out var list)) building[term] = list = [];
                list.Add(at);
            }
        }

        terms = [.. building.Keys.Order(StringComparer.Ordinal)];
        postings = new int[terms.Length][];
        for (var at = 0; at < terms.Length; at++) postings[at] = [.. building[terms[at]]];

        carrying = docs
            .Select((document, at) => (document.Id, At: at))
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.Select(row => row.At).ToArray());

        logger.LogInformation(
            "Search index built: {Documents} documents, {Reachable} named and searchable, {Terms} terms, {Postings} postings",
            docs.Length, reachable, terms.Length, postings.Sum(p => p.Length));
    }

    public const int IdScore = 100_000;

    public IReadOnlyList<Hit> Search(string text, int limit)
    {
        var numbered = int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
            carrying.TryGetValue(id, out var found)
                ? found
                : [];

        var typed = Words(text).Distinct(StringComparer.Ordinal).ToArray();

        HashSet<int> candidates = [];

        for (var at = 0; at < typed.Length; at++)
        {
            var reached = Reach(typed[at]);

            if (at == 0) candidates = reached;
            else candidates.IntersectWith(reached);

            if (candidates.Count == 0) break;
        }

        if (numbered.Length == 0 && candidates.Count == 0) return [];

        var whole = text.Trim().ToLowerInvariant();

        var scored = new Dictionary<int, int>(candidates.Count + numbered.Length);

        foreach (var doc in candidates) scored[doc] = Score(doc, typed, whole);
        foreach (var doc in numbered) scored[doc] = IdScore;

        return
        [
            .. scored
                .Select(pair => new Hit(docs[pair.Key], pair.Value))
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => PublicTypes.NameOf(hit.Document).Length)
                .ThenBy(hit => PublicTypes.NameOf(hit.Document), StringComparer.OrdinalIgnoreCase)
                .ThenBy(hit => hit.Document.Id)
                .Take(limit),
        ];
    }

    private HashSet<int> Reach(string prefix)
    {
        var reached = new HashSet<int>();

        for (var at = LowerBound(prefix);
             at < terms.Length && terms[at].StartsWith(prefix, StringComparison.Ordinal);
             at++)
        {
            foreach (var doc in postings[at]) reached.Add(doc);
        }

        return reached;
    }

    private int LowerBound(string prefix)
    {
        int low = 0, high = terms.Length;

        while (low < high)
        {
            var middle = (low + high) / 2;
            if (string.CompareOrdinal(terms[middle], prefix) < 0) low = middle + 1;
            else high = middle;
        }

        return low;
    }

    private int Score(int doc, IReadOnlyList<string> typed, string whole)
    {
        var name = display[doc];
        var score = 0;

        if (name == whole) score += 2000;
        else if (name.StartsWith(whole, StringComparison.Ordinal)) score += 900;
        else if (name.Contains(whole, StringComparison.Ordinal)) score += 400;

        foreach (var word in typed)
        {
            if (name.StartsWith(word, StringComparison.Ordinal)) score += 120;
            else if (Starts(displayWords[doc], word)) score += 70;
        }

        if (DocumentFields.Text(docs[doc], nameof(ItemDocument.Icon)) is not null) score += 15;
        if (DocumentFields.Text(docs[doc], nameof(ItemDocument.Description)) is not null) score += 10;
        if (docs[doc] is EffectDocument { NameBorrowedFrom: not null }) score -= 45;

        return score;
    }

    private static bool Starts(string[] words, string prefix)
    {
        foreach (var word in words)
        {
            if (word.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    public static IEnumerable<string> Words(string text)
    {
        var start = -1;

        for (var at = 0; at <= text.Length; at++)
        {
            var wordChar = at < text.Length && char.IsLetterOrDigit(text[at]);

            if (wordChar)
            {
                if (start < 0) start = at;
                continue;
            }

            if (start < 0) continue;

            foreach (var part in Parts(text[start..at])) yield return part;
            start = -1;
        }
    }

    private static IEnumerable<string> Parts(string run)
    {
        yield return run.ToLowerInvariant();

        var start = 0;

        for (var at = 1; at <= run.Length; at++)
        {
            if (at < run.Length && !(char.IsUpper(run[at]) && !char.IsUpper(run[at - 1]))) continue;

            if (start > 0 && at - start > 1) yield return run[start..at].ToLowerInvariant();

            start = at;
        }
    }

    public readonly record struct Hit(EntityDocument Document, int Score);
}
