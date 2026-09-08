using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public static class Stats
{
    public static IReadOnlyDictionary<string, StatRolls> Rated(string stat, params decimal[] values) =>
        With(stat, rolls => rolls with { Rating = values });

    public static IReadOnlyDictionary<string, StatRolls> Percent(string stat, params decimal[] values) =>
        With(stat, rolls => rolls with { Percentage = values });

    private static IReadOnlyDictionary<string, StatRolls> With(
        string stat, Func<StatRolls, StatRolls> change) =>
        new Dictionary<string, StatRolls>(StringComparer.OrdinalIgnoreCase)
        {
            [stat] = change(new StatRolls()),
        };
}
