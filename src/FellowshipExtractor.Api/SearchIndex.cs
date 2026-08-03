using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// The name index the omnibox runs against: every word of every display name a public row carries,
/// as one sorted term list with a posting list per term.
///
/// Display names, and nothing else. The other name a row has is its gameplay tag, which is the
/// build's internal spelling of it and is shown to a reader nowhere, so it is searched for a reader
/// nowhere either: "ga_mara" reaches nothing, and neither does a hero's codename or a tag table's
/// name, because no term in here was ever written that way. Dropping the tags takes the term list
/// from 6,313 terms to 2,336.
///
/// A display name only counts as one where <see cref="PublicTypes.DisplayNameOf"/> says it is one,
/// which is what keeps the build's own labels out of the box: the 48 rows named
/// UB_CadavrianBannerman.WarHorn, AutoAttack, CastProjectile, RuinsLasher, Playtest GearToken and
/// the like answered "warhorn", "autoattack" and "playtest" outright, and now contribute no term at
/// all, the same as a row shipping no name.
///
/// The consequence is the point. 7,957 of the 14,478 public rows carry no name by that test, so they
/// contribute no term at all and cannot be reached by any spelling of anything: they leave search by
/// construction rather than by a filter downstream that somebody has to remember to apply. The rows
/// of a non-public table are held out of the corpus the same way, one Where at build time rather
/// than a test per query, which is 757 rows over 12 tables a reader is never shown.
///
/// Built rather than scanned because a display name is several words a reader might type any one of,
/// and a CamelCase one is several more inside a single run. Splitting a name into its words at build
/// time, including at every capital, turns what would be a substring scan over five thousand names
/// on every keystroke into a binary search over the terms that begin with what was typed.
///
/// Prefix rather than exact, because the box answers while the reader is still typing: "thund"
/// reaches Thunderlord before the word is finished. Several typed words all have to match, each by
/// prefix, which is what makes "fire ball" find Fire Ball without also answering everything carrying
/// either word on its own.
/// </summary>
public sealed class SearchIndex
{
    private readonly Entity[] docs;

    /// <summary>Each row's display name, lower cased, for the whole-phrase tests.</summary>
    private readonly string[] display;

    /// <summary>The words each name is made of, kept so scoring never re-tokenizes.</summary>
    private readonly string[][] displayWords;

    /// <summary>The terms, sorted, so a prefix is a contiguous run found by binary search.</summary>
    private readonly string[] terms;

    /// <summary>Which documents each term appears in, aligned with <see cref="terms"/>.</summary>
    private readonly int[][] postings;

    public int Documents => docs.Length;

    public int Terms => terms.Length;

    public SearchIndex(EntityStore store, ILogger<SearchIndex> logger)
    {
        docs = [.. store.Entities.Where(entity => PublicTypes.IsPublic(entity.Table))];

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

        // Both ends of the narrowing, because either one silently going wrong is a search that
        // answers rows a reader should not be shown or misses rows they should.
        logger.LogInformation(
            "Search index built: {Documents} of {Rows} rows are of a public type, {Reachable} of " +
            "those are named and searchable, {Terms} terms, {Postings} postings",
            docs.Length, store.Entities.Count, reachable, terms.Length, postings.Sum(p => p.Length));
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

        // Every hit is named, because a term is a word of a name and a row without one is in no
        // posting list, so the public name and the display name are the same string here. The
        // shorter of two names that both answer what was typed is the closer match, and the id
        // decides between two readings of one thing so the order is stable.
        return
        [
            .. hits
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => PublicTypes.NameOf(hit.Entity).Length)
                .ThenBy(hit => PublicTypes.NameOf(hit.Entity), StringComparer.OrdinalIgnoreCase)
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
    /// things that merely contain it, and under all three a rung per typed word, so a name that
    /// answers two of the words outranks one that answers one.
    ///
    /// There is no rung for having a name any more. Every row that can be reached has one, so the
    /// bonus that used to lift a named row above a bare tag would now be a constant added to every
    /// score in the list.
    ///
    /// The one deduction is for a name the row did not ship. 1,288 effects are named by the entity
    /// that applies them, and the borrowed word is the same word: typing "attack" reaches the 123
    /// effects called Attack and the abilities they took it from together, and a reader who types a
    /// name means the thing that owns it. The deduction is sized to settle exactly that tie and
    /// nothing else - larger than the 25 the art and description bonuses can add between them, so a
    /// borrowed name never wins on having an icon, and smaller than the 70 that separates one rung
    /// of match quality from the next, so it can never push a better textual match below a worse one.
    /// </summary>
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

        if (docs[doc].Icon is not null) score += 15;
        if (docs[doc].Description is not null) score += 10;
        if (docs[doc].NameInheritedFrom is not null) score -= 45;

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
    /// capital-started part inside it, because MeleeAutoAttack is three words a reader might type any
    /// one of. The whole run is always kept as well, so the shipped spelling still matches.
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
    /// lookup is by prefix. A part of a single character is dropped, because nobody searches on one
    /// letter and a term that short costs a posting list holding a good share of the corpus.
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
