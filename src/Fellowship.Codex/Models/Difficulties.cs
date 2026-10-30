using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Models;

public sealed record DifficultyStep(int Difficulty, string League, int Step, string Label);

public sealed record DamageBand(decimal Mean, decimal Low, decimal High);

public static class Difficulties
{
    public const string PrimaryAttribute = "PrimaryAttribute";

    public static IReadOnlyList<DifficultyStep> Ladder(SettingsFile settings) =>
        [.. settings.DifficultyScaling.Keys.Order().Select(difficulty => Step(settings, difficulty))];

    public static IReadOnlyList<DifficultyStep> Ladder(SettingsFile settings, IReadOnlyList<DungeonDocument> dungeons)
    {
        var ladder = Ladder(settings);

        if (dungeons.All(dungeon => dungeon.Difficulties is not { Count: > 0 } && dungeon.AvailableForCasual is not true))
        {
            return ladder;
        }

        var casual = settings.Leagues
            .Where(league => league.Kind == LeagueKind.Casual)
            .SelectMany(league => league.Difficulties)
            .ToArray();

        var run = dungeons
            .SelectMany(dungeon => (dungeon.Difficulties ?? []).Concat(dungeon.AvailableForCasual is true ? casual : []))
            .ToHashSet();

        return [.. ladder.Where(step => run.Contains(step.Difficulty))];
    }

    public static DifficultyStep? At(SettingsFile settings, int difficulty) =>
        settings.DifficultyScaling.ContainsKey(difficulty) ? Step(settings, difficulty) : null;

    public static int? Read(string? value, SettingsFile settings) =>
        int.TryParse(value, out var difficulty) && settings.DifficultyScaling.ContainsKey(difficulty)
            ? difficulty
            : null;

    private static DifficultyStep Step(SettingsFile settings, int difficulty)
    {
        var label = settings.DifficultyScaling[difficulty].Label;

        if (Covering(settings, difficulty) is not { } league)
        {
            return new DifficultyStep(difficulty, string.Empty, difficulty, label);
        }

        var step = league.Difficulties.TakeWhile(listed => listed != difficulty).Count() + 1;

        return new DifficultyStep(difficulty, league.Name, step, label);
    }

    private static LeagueEntry? Covering(SettingsFile settings, int difficulty)
    {
        LeagueEntry? found = null;

        foreach (var league in settings.Leagues)
        {
            if (league.Kind is LeagueKind.Pinnacle or LeagueKind.Casual) continue;
            if (!league.Difficulties.Contains(difficulty)) continue;
            if (found is not null && found.MinimumDifficulty >= league.MinimumDifficulty) continue;

            found = league;
        }

        return found;
    }

    public static double? HealthScale(SettingsFile settings, int difficulty) =>
        settings.DifficultyScaling.TryGetValue(difficulty, out var level) ? level.Health : null;

    public static double? DamageScale(SettingsFile settings, int difficulty) =>
        settings.DifficultyScaling.TryGetValue(difficulty, out var level) ? level.Damage : null;

    public static double? Health(CreatureDocument creature, SettingsFile settings, int difficulty) =>
        creature.Health is { } health && HealthScale(settings, difficulty) is { } scale
            ? health * scale
            : null;

    public static double? Attribute(CreatureDocument creature, string attribute) => attribute switch
    {
        nameof(CreatureDocument.Intellect) => creature.Intellect,
        nameof(CreatureDocument.Strength) => creature.Strength,
        nameof(CreatureDocument.Agility) => creature.Agility,
        PrimaryAttribute => Greatest(creature),
        _ => null,
    };

    private static double? Greatest(CreatureDocument creature)
    {
        double? found = null;

        foreach (var reading in (double?[])[creature.Intellect, creature.Strength, creature.Agility])
        {
            if (reading is { } value && (found is null || value > found)) found = value;
        }

        return found;
    }

    public static DamageBand? Damage(
        AbilityCoefficient coefficient,
        CreatureDocument owner,
        SettingsFile settings,
        int difficulty)
    {
        if (Attribute(owner, coefficient.Attribute) is not { } attribute) return null;
        if (DamageScale(settings, difficulty) is not { } scale) return null;

        var mean = (decimal)attribute * (decimal)coefficient.Value * (decimal)scale;
        var half = (decimal)(coefficient.Spread ?? 0) / 2m;

        return new DamageBand(mean, mean * (1m - half), mean * (1m + half));
    }
}
