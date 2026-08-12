using System.Globalization;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class StatQuery
{
    private const decimal Ceiling = 1_000_000_000_000m;

    public static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static TooltipQuery Read<TRequest>(
        TRequest request, IQueryCollection raw, StatParameters stats, out string? error)
        where TRequest : notnull
    {
        error = null;

        var declared = RequestProperties.Of(request.GetType());

        var values = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, given) in raw)
        {
            if (declared.Contains(key)) continue;

            var stat = stats.Resolve(key);
            var percentage = false;

            if (stat is null && key.EndsWith(StatConversion.PercentageSuffix, StringComparison.OrdinalIgnoreCase))
            {
                stat = stats.Resolve(key[..^StatConversion.PercentageSuffix.Length]);
                percentage = stat is not null;
            }

            if (stat is null)
            {
                error = $"Unknown parameter '{key}'.";
                return new TooltipQuery();
            }

            var attribute = stats.Row(stat);

            if (percentage && !StatConversion.Converts(attribute))
            {
                error =
                    $"'{key[..^StatConversion.PercentageSuffix.Length]}' is not a rating, " +
                    $"so '{key}' has nothing to convert.";
                return new TooltipQuery();
            }

            var numbers = new List<decimal>(given.Count);

            foreach (var value in given)
            {
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    error = $"Stat '{key}' has the non-numeric value '{value}'.";
                    return new TooltipQuery();
                }

                if (Math.Abs(number) > Ceiling)
                {
                    error = $"Stat '{key}' is {value}, past the {Ceiling:N0} a coefficient can be applied to.";
                    return new TooltipQuery();
                }

                numbers.Add(number);
            }

            if (!percentage)
            {
                values[stat] = values.TryGetValue(stat, out var already) ? [.. already, .. numbers] : numbers;
            }

            attributes[stat] = attributes.GetValueOrDefault(stat) + (percentage
                ? StatConversion.FromPercentage(numbers.Sum(), attribute)
                : StatConversion.FromRating(numbers.Sum(), attribute));
        }

        return new TooltipQuery { Stats = values, Attributes = attributes };
    }

    public static IReadOnlyList<string> Rolled(
        RolledStats? given, StatParameters stats, IReadOnlyList<string> pool, out string? error)
    {
        error = null;

        if (given is not { Values.Count: > 0 }) return [];

        var rollable = new HashSet<string>(pool, StringComparer.OrdinalIgnoreCase);
        var resolved = new List<string>(given.Values.Count);

        foreach (var name in given.Values)
        {
            var stat = stats.Resolve(name) ?? (rollable.Contains(name) ? name : null);

            if (stat is null || !rollable.Contains(stat))
            {
                error =
                    $"'{name}' is not a stat a random slot rolls. " +
                    $"The pool is {string.Join(", ", pool)}.";
                return [];
            }

            resolved.Add(rollable.TryGetValue(stat, out var filed) ? filed : stat);
        }

        return resolved;
    }

    public static IResult Unreadable(string error, StatParameters stats) => Results.Problem(
        title: "The request parameters could not be read.",
        detail: $"{error} Stat parameters are {string.Join(", ", stats.Names)}.",
        statusCode: StatusCodes.Status400BadRequest);
}
