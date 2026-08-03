using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// The name index the omnibox runs against: every word of every display name and every segment of
/// every gameplay tag, as one sorted term list with a posting list per term.
///
/// Built rather than scanned because both halves of what a reader types have to work. A tag is
/// written as dotted CamelCase - GA_Firemage_CastedPlacedAoe - so "projectile" has to find
/// PlacedProjectileAoe, and a linear substring scan finds that only by looking inside every one of
/// the corpus's fourteen thousand names on every keystroke. Splitting a name into its words at build
/// time, including at every capital, turns that into a binary search over the terms that begin with
/// what was typed.
///
/// Prefix rather than exact, because the box answers while the reader is still typing: "thund"
/// reaches Thunderlord before the word is finished. Several typed words all have to match, each by
/// prefix, which is what makes "fire ball" find Fire Ball without also answering everything carrying
/// either word on its own.
/// </summary>
public sealed class SearchIndex
{
    private readonly Entity[] docs;

    /// <summary>The display name and the tag name, lower cased, for the whole-phrase tests.</summary>
    private readonly string[] display;
    private readonly string[] tags;

    /// <summary>The words each name is made of, kept so scoring never re-tokenizes.</summary>
    private readonly string[][] displayWords;
    private readonly string[][] tagWords;

    /// <summary>The terms, sorted, so a prefix is a contiguous run found by binary search.</summary>
    private readonly string[] terms;

    /// <summary>Which documents each term appears in, aligned with <see cref="terms"/>.</summary>
    private readonly int[][] postings;

    public int Documents => docs.Length;

    public int Terms => terms.Length;

    public SearchIndex(EntityStore store, ILogger<SearchIndex> logger)
    {
        docs = [.. store.Entities];
        display = new string[docs.Length];
        tags = new string[docs.Length];
        displayWords = new string[docs.Length][];
        tagWords = new string[docs.Length][];

        var building = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var at = 0; at < docs.Length; at++)
        {
            var name = docs[at].DisplayName ?? string.Empty;
            var tag = docs[at].Name ?? string.Empty;

            display[at] = name.ToLowerInvariant();
            tags[at] = tag.ToLowerInvariant();
            displayWords[at] = [.. Words(name)];
            tagWords[at] = [.. Words(tag)];

            foreach (var term in displayWords[at].Concat(tagWords[at]).Distinct(StringComparer.Ordinal))
            {
                if (!building.TryGetValue(term, out var list)) building[term] = list = [];
                list.Add(at);
            }
        }

        terms = [.. building.Keys.Order(StringComparer.Ordinal)];
        postings = new int[terms.Length][];
        for (var at = 0; at < terms.Length; at++) postings[at] = [.. building[terms[at]]];

        logger.LogInformation(
            "Search index built: {Documents} documents, {Terms} terms, {Postings} postings",
            docs.Length, terms.Length, postings.Sum(p => p.Length));
    }

    /// <summary>
    /// The rows a reader's words reach, best first. Every typed word has to match something, so a
    /// second word narrows rather than widens.
    /// </summary>
    public IReadOnlyList<Hit> Search(string text, int limit)
    {
        var typed = Words(text).Distinct(StringComparer.Ordinal).ToArray();
        if (typed.Length == 0) return [];

        HashSet<int>? candidates = null;

        foreach (var word in typed)
        {
            var reached = Reach(word);

            if (candidates is null) candidates = reached;
            else candidates.IntersectWith(reached);

            if (candidates.Count == 0) return [];
        }

        var whole = text.Trim().ToLowerInvariant();

        var hits = new List<Hit>(candidates!.Count);
        foreach (var doc in candidates) hits.Add(new Hit(docs[doc], Score(doc, typed, whole)));

        return
        [
            .. hits
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => (hit.Entity.DisplayName ?? hit.Entity.Name ?? string.Empty).Length)
                .ThenBy(hit => hit.Entity.DisplayName ?? hit.Entity.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(hit => hit.Entity.NetId)
                .Take(limit),
        ];
    }

    /// <summary>Every document holding a term that begins with the typed word.</summary>
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

    /// <summary>
    /// How well one row answers what was typed. The ladder is what a reader expects of a search box:
    /// the thing they named outright first, then the things whose name starts that way, then the
    /// things that merely contain it, and a row carrying a real display name ahead of a bare gameplay
    /// tag at every rung, because the tag is the internal spelling of something that usually has a
    /// name of its own further down the same list.
    /// </summary>
    private int Score(int doc, IReadOnlyList<string> typed, string whole)
    {
        var name = display[doc];
        var score = 0;

        if (name.Length > 0)
        {
            score += 60;

            if (name == whole) score += 2000;
            else if (name.StartsWith(whole, StringComparison.Ordinal)) score += 900;
            else if (name.Contains(whole, StringComparison.Ordinal)) score += 400;
        }

        // The last segment of a tag is the entity's own name within its table, so a tag ending in
        // what was typed is that thing rather than something merely filed under it.
        if (tags[doc].EndsWith('.' + whole, StringComparison.Ordinal)) score += 500;

        foreach (var word in typed)
        {
            if (name.Length > 0)
            {
                if (name.StartsWith(word, StringComparison.Ordinal)) score += 120;
                else if (Starts(displayWords[doc], word)) score += 70;
            }

            if (Starts(tagWords[doc], word)) score += 25;
        }

        if (docs[doc].Icon is not null) score += 15;
        if (docs[doc].Description is not null) score += 10;

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

    /// <summary>
    /// The words a name is made of, lower cased. A run of letters and digits is a word; so is each
    /// capital-started part inside it, because PlacedProjectileAoe is three words a reader might type
    /// any one of. The whole run is always kept as well, so the shipped spelling still matches.
    /// </summary>
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

    /// <summary>
    /// One run of letters and digits: itself, and its capital-started parts after the first. The
    /// opening part needs no term of its own because the whole run already begins with it and every
    /// lookup is by prefix. A part of a single character is dropped, so the C in C_Crit contributes
    /// nothing rather than a posting list holding half the corpus.
    /// </summary>
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

    public readonly record struct Hit(Entity Entity, int Score);
}
