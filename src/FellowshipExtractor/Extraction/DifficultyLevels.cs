namespace FellowshipExtractor.Extraction;

public static class DifficultyLevels
{
    public const string Base = "Base";

    public static IReadOnlyDictionary<int, DifficultyLevel> Named(
        CreatureStats creatures, DungeonScoring scoring, IReadOnlyList<LeagueEntry> leagues)
    {
        var named = new Dictionary<int, DifficultyLevel>();

        foreach (var difficulty in creatures.Scaling.Health.Keys.Order())
        {
            var label = Label(difficulty, leagues);

            named[difficulty] = new DifficultyLevel
            {
                Name = label.ToLowerInvariant(),
                Label = label,
                Health = creatures.Scaling.Health[difficulty],
                Damage = creatures.Scaling.Damage[difficulty],
                MaxDungeonScore = scoring.MaxDungeonScore.TryGetValue(difficulty, out var score) ? score : null,
            };
        }

        var repeated = named.Values.GroupBy(level => level.Name).FirstOrDefault(group => group.Count() > 1);

        if (repeated is not null)
        {
            throw new InvalidOperationException(
                $"Difficulties {string.Join(", ", named.Where(entry => entry.Value.Name == repeated.Key).Select(entry => entry.Key))} " +
                $"are all named '{repeated.Key}'.");
        }

        return named;
    }

    private static string Label(int difficulty, IReadOnlyList<LeagueEntry> leagues)
    {
        LeagueEntry? found = null;

        foreach (var league in leagues)
        {
            if (league.Kind is LeagueKind.Pinnacle or LeagueKind.Casual) continue;
            if (!league.Difficulties.Contains(difficulty)) continue;
            if (found is not null && found.MinimumDifficulty >= league.MinimumDifficulty) continue;

            found = league;
        }

        return found is null
            ? Base
            : $"{found.Name} {found.Difficulties.TakeWhile(listed => listed != difficulty).Count() + 1}";
    }
}
