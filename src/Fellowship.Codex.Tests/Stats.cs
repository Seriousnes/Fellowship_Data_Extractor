using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public static class Stats
{
    public static IReadOnlyDictionary<string, StatRolls> Rated(string stat, params decimal[] values) =>
        Rated(new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase), stat, values);

    public static IReadOnlyDictionary<string, StatRolls> Rated(
        this IReadOnlyDictionary<string, StatRolls> stats, string stat, params decimal[] values) =>
        With(stats, stat, rolls => rolls with { Rating = values });

    public static IReadOnlyDictionary<string, StatRolls> Percent(string stat, params decimal[] values) =>
        Percent(new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase), stat, values);

    public static IReadOnlyDictionary<string, StatRolls> Percent(
        this IReadOnlyDictionary<string, StatRolls> stats, string stat, params decimal[] values) =>
        With(stats, stat, rolls => rolls with { Percentage = values });

    private static IReadOnlyDictionary<string, StatRolls> With(
        IReadOnlyDictionary<string, StatRolls> stats, string stat, Func<StatRolls, StatRolls> change)
    {
        var written = new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, rolls) in stats) written[name] = rolls;

        written[stat] = change(written.GetValueOrDefault(stat) ?? new StatRolls());

        return written;
    }
}
