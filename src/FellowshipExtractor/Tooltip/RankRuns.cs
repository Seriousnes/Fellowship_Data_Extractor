using System.Globalization;
using System.Text.RegularExpressions;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The four-value progressions printed in an item trait's description, bound to the tuning rows
/// that produce them.
///
/// Nothing substitutes these at runtime. A trait description contains no tokens at all: the slash
/// list "4% / 6% / 8% / 11%" is characters a designer typed, and so is "(2.6 PPM)". The same string
/// ships again in the locres, unchanged. So the extractor's job is not to render a run, it is to
/// say which shipped curve row a run was typed from, so that a reader wanting one rank can pick it
/// and a reader wanting the number can compute with it.
///
/// Which arithmetic turns a curve value into its printed digits is nowhere in the build. Five are
/// in use, so the binding is a search and not a rule: a row is claimed only when one arithmetic
/// reproduces all four printed values, or three of the four with the disagreement recorded. That
/// the numbers have to agree is also what makes the search safe, because a row picked for the wrong
/// reason produces no agreement at all.
/// </summary>
public static partial class RankRuns
{
    private const int Ranks = 4;

    /// <summary>
    /// How many ticks a per-tick value may be totalled over. Searched rather than computed: the
    /// three shipped cases total 7, 4 and 5 ticks against durations and periods that give 6, 3 and
    /// 5.33, so no arithmetic on the shipped Duration and Period reproduces all three.
    /// </summary>
    private const int MaxTicks = 20;

    /// <summary>
    /// One printed value: either a whole rich-text span with a number inside it, or a bare number
    /// with an optional percent sign. Both forms ship, sometimes in the same table - Amethyst
    /// Splinters wraps each of its four values and Stalwart Readiness wraps all four together - and
    /// the span is captured whole so that substituting a rank keeps the styling the sentence around
    /// it uses.
    /// </summary>
    private const string Group = @"(<rt\.[^>]*>[^<]*?-?\d+(?:\.\d+)?[^<]*?</>|(?<![\d.])-?\d+(?:\.\d+)?%?)";

    private static readonly string[] Transforms = ["raw", "delta", "reduction", "plain"];

    [GeneratedRegex(Group + " / " + Group + " / " + Group + " / " + Group)]
    private static partial Regex Run { get; }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex Number { get; }

    /// <summary>
    /// The runs a description prints, in the order they appear, each bound to a row of the supplied
    /// candidates where one of them reproduces it. A run that matches nothing keeps its printed
    /// values and carries no row, because a guessed row is worse than none.
    /// </summary>
    public static IReadOnlyList<RankRun>? For(string? description, IReadOnlyList<ConstantRecord> candidates)
    {
        if (description is null) return null;

        var printed = Parse(description);
        if (printed.Count == 0) return null;

        var attempts = printed.Select(run => Rank(run, candidates)).ToArray();
        var chosen = new Match?[printed.Count];
        var taken = new HashSet<string>(StringComparer.Ordinal);

        // Exact matches are assigned before near ones, so a run that only nearly fits a row cannot
        // take the row the next run fits exactly. Two runs in one description routinely fit two
        // rows holding the same four numbers - Kindling's burn and its cauterise are the same
        // curve twice - which is why a claimed row is off the table for the runs after it.
        foreach (var wanted in new[] { Ranks, Ranks - 1 })
        {
            for (var i = 0; i < printed.Count; i++)
            {
                if (chosen[i] is not null) continue;

                foreach (var attempt in attempts[i])
                {
                    if (attempt.Hits != wanted || !taken.Add(attempt.Record.Key)) continue;

                    chosen[i] = attempt;
                    break;
                }
            }
        }

        return [.. printed.Select((run, i) => Bind(run, chosen[i]))];
    }

    private static RankRun Bind(Printed run, Match? match)
    {
        var bound = new RankRun(run.Literal, run.Groups);
        if (match is not { } found) return bound;

        return bound with
        {
            Values = found.Record.Values,
            Key = found.Record.Key,
            Transform = found.Transform,
            Ticks = found.Ticks,
            Disagrees = found.Hits < Ranks ? true : null,
        };
    }

    private static List<Printed> Parse(string description)
    {
        var found = new List<Printed>();

        foreach (System.Text.RegularExpressions.Match match in Run.Matches(description))
        {
            var groups = new string[Ranks];
            var numbers = new string[Ranks];
            var complete = true;

            for (var i = 0; i < Ranks; i++)
            {
                groups[i] = match.Groups[i + 1].Value;
                var number = Number.Match(groups[i]);
                if (!number.Success) { complete = false; break; }
                numbers[i] = number.Value;
            }

            if (complete) found.Add(new Printed(match.Value, groups, numbers));
        }

        return found;
    }

    /// <summary>
    /// Every candidate row's best showing against one run, in table order. The order is load
    /// bearing and must not be sorted: two rows holding the same four numbers both fit the same run
    /// exactly - Amethyst Splinters' debuff and buff are one curve twice, and so are Kindling's -
    /// and only the order the table declares them in separates the first run's row from the
    /// second's.
    /// </summary>
    private static List<Match> Rank(Printed run, IReadOnlyList<ConstantRecord> candidates)
    {
        var attempts = new List<Match>();

        foreach (var record in candidates)
        {
            if (record.Values is not { Length: Ranks } values) continue;
            if (values.Any(value => value is null)) continue;

            Match? best = null;

            foreach (var transform in Transforms)
            {
                var hits = Hits(run, values, value => Apply(transform, value, 1));
                if (best is null || hits > best.Value.Hits) best = new Match(record, transform, null, hits);
                if (hits == Ranks) break;
            }

            for (var ticks = 2; ticks <= MaxTicks && best?.Hits != Ranks; ticks++)
            {
                if (Hits(run, values, value => Apply("ticks", value, ticks)) == Ranks)
                {
                    best = new Match(record, "ticks", ticks, Ranks);
                }
            }

            if (best is { Hits: > 0 }) attempts.Add(best.Value);
        }

        return attempts;
    }

    private static double Apply(string transform, double value, int ticks) => transform switch
    {
        "raw" => value * 100,
        "delta" => (value - 1) * 100,
        "reduction" => (1 - value) * 100,
        "ticks" => value * ticks * 100,
        _ => value,
    };

    private static int Hits(Printed run, double?[] values, Func<double, double> transform)
    {
        var hits = 0;

        for (var i = 0; i < Ranks; i++)
        {
            if (values[i] is { } value && Agrees(run.Numbers[i], transform(value))) hits++;
        }

        return hits;
    }

    /// <summary>
    /// Whether a computed number is the one printed, judged at the precision the text was written
    /// to. Rounding is the whole reason this is not equality: 1.095 under delta is 9.499999999999
    /// and the sentence says 9.5%.
    /// </summary>
    private static bool Agrees(string printed, double computed)
    {
        var point = printed.IndexOf('.');
        var places = point < 0 ? 0 : printed.Length - point - 1;

        return Math.Abs(Math.Round(computed, places, MidpointRounding.AwayFromZero) -
            double.Parse(printed, CultureInfo.InvariantCulture)) < 1e-9;
    }

    private readonly record struct Printed(string Literal, string[] Groups, string[] Numbers);

    private readonly record struct Match(ConstantRecord Record, string Transform, int? Ticks, int Hits);
}
