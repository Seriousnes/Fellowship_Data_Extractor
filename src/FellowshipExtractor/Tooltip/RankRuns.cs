using System.Globalization;
using System.Text.RegularExpressions;

namespace FellowshipExtractor.Tooltip;

public static partial class RankRuns
{
    private const int Ranks = 4;

    private const int MaxTicks = 20;

    private const string Group = @"(<rt\.[^>]*>[^<]*?-?\d+(?:\.\d+)?[^<]*?</>|(?<![\d.])-?\d+(?:\.\d+)?%?)";

    private static readonly string[] Transforms = ["raw", "delta", "reduction", "plain"];

    [GeneratedRegex(Group + " / " + Group + " / " + Group + " / " + Group)]
    private static partial Regex Run { get; }

    [GeneratedRegex(@"-?\d+(?:\.\d+)?")]
    private static partial Regex Number { get; }

    public static IReadOnlyList<RankRun>? For(string? description, IReadOnlyList<ConstantRecord> candidates)
    {
        if (description is null) return null;

        var printed = Parse(description);
        if (printed.Count == 0) return null;

        var attempts = printed.Select(run => Rank(run, candidates)).ToArray();
        var chosen = new Match?[printed.Count];
        var taken = new HashSet<string>(StringComparer.Ordinal);

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
