using System.Globalization;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Endpoints;

public static class StatQuery
{
    private const decimal Ceiling = 1_000_000_000_000m;

    public static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static TooltipQuery Read(
        IReadOnlyDictionary<string, StatRolls>? stats, StatParameters parameters, out string? error)
    {
        error = null;

        if (stats is null) return new TooltipQuery();

        var values = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, rolls) in stats)
        {
            var stat = parameters.Resolve(key);

            if (stat is null)
            {
                error = $"Unknown stat '{key}'.";
                return new TooltipQuery();
            }

            var attribute = parameters.Row(stat);

            IReadOnlyList<decimal> rating = rolls?.Rating ?? [];
            IReadOnlyList<decimal> percentage = rolls?.Percentage ?? [];

            if (percentage.Count > 0 && !StatConversion.Converts(attribute))
            {
                error = $"'{key}' is not a rating, so its percentage rolls have nothing to convert.";
                return new TooltipQuery();
            }

            if (rating.Count == 0 && percentage.Count == 0) continue;

            error = TooLarge(key, rating) ?? TooLarge(key, percentage);

            if (error is not null) return new TooltipQuery();

            if (rating.Count > 0)
            {
                values[stat] = values.TryGetValue(stat, out var existing) ? [.. existing, .. rating] : rating;
            }

            attributes[stat] = attributes.GetValueOrDefault(stat)
                + StatConversion.FromRating(rating.Sum(), attribute)
                + StatConversion.FromPercentage(percentage.Sum(), attribute);
        }

        return new TooltipQuery { Stats = values, Attributes = attributes };
    }

    public static IReadOnlyDictionary<string, StatRolls> FromQuery(
        IQueryCollection query, IReadOnlySet<string> declared, StatParameters parameters, out string? error)
    {
        error = null;

        var stats = new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in query)
        {
            if (declared.Contains(key)) continue;

            var name = key;
            var stat = parameters.Resolve(key);
            var percentage = false;

            if (stat is null && key.EndsWith(StatConversion.PercentageSuffix, StringComparison.OrdinalIgnoreCase))
            {
                name = key[..^StatConversion.PercentageSuffix.Length];
                stat = parameters.Resolve(name);
                percentage = stat is not null;
            }

            if (stat is null)
            {
                error = $"Unknown parameter '{key}'.";
                return new Dictionary<string, StatRolls>();
            }

            if (percentage && !StatConversion.Converts(parameters.Row(stat)))
            {
                error = $"'{name}' is not a rating, so '{key}' has nothing to convert.";
                return new Dictionary<string, StatRolls>();
            }

            var numbers = new List<decimal>(values.Count);

            foreach (var value in values)
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    error = $"Stat '{key}' has the non-numeric value '{value}'.";
                    return new Dictionary<string, StatRolls>();
                }

                numbers.Add(number);
            }

            var rolls = stats.GetValueOrDefault(name) ?? new StatRolls();
            var existing = percentage ? rolls.Percentage : rolls.Rating;

            IReadOnlyList<decimal> joined = existing is null ? numbers : [.. existing, .. numbers];

            stats[name] = percentage ? rolls with { Percentage = joined } : rolls with { Rating = joined };
        }

        return stats;
    }

    public static IReadOnlyList<string> Rolled(
        IReadOnlyList<string>? names, StatParameters parameters, IReadOnlyList<string> pool, out string? error)
    {
        error = null;

        if (names is not { Count: > 0 }) return [];

        var rollable = new HashSet<string>(pool, StringComparer.OrdinalIgnoreCase);
        var resolved = new List<string>(names.Count);

        foreach (var name in names)
        {
            var stat = parameters.Resolve(name) ?? (rollable.Contains(name) ? name : null);

            if (stat is null || !rollable.Contains(stat))
            {
                error =
                    $"'{name}' is not a stat a random slot rolls. " +
                    $"The pool is {string.Join(", ", pool)}.";
                return [];
            }

            resolved.Add(rollable.TryGetValue(stat, out var stored) ? stored : stat);
        }

        return resolved;
    }

    public static IResult Unreadable(string error, StatParameters parameters) => Results.Problem(
        title: "The request parameters could not be read.",
        detail: $"{error} Stat parameters are {string.Join(", ", parameters.Names)}.",
        statusCode: StatusCodes.Status400BadRequest);

    private static string? TooLarge(string key, IReadOnlyList<decimal> values)
    {
        foreach (var value in values)
        {
            if (Math.Abs(value) > Ceiling)
            {
                return
                    $"Stat '{key}' is {value.ToString(CultureInfo.InvariantCulture)}, " +
                    $"past the {Ceiling:N0} a coefficient can be applied to.";
            }
        }

        return null;
    }
}
