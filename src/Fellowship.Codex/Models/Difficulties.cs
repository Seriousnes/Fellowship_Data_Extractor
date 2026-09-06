using Fellowship.SDK;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Models;

public sealed record DifficultyStep(int Difficulty, string League, int Step, string Label);

public sealed record DamageBand(decimal Mean, decimal Low, decimal High);

public static class Difficulties
{
    public const string PrimaryAttribute = "PrimaryAttribute";

    public static IReadOnlyList<DifficultyStep> Ladder(SettingsFile settings) =>
        [.. settings.DifficultyScaling.Health.Keys.Order().Select(difficulty => Step(settings, difficulty))];

    public static DifficultyStep? At(SettingsFile settings, int difficulty) =>
        settings.DifficultyScaling.Health.ContainsKey(difficulty) ? Step(settings, difficulty) : null;

    public static int? Read(string? value, SettingsFile settings) =>
        int.TryParse(value, out var difficulty) && settings.DifficultyScaling.Health.ContainsKey(difficulty)
            ? difficulty
            : null;

    private static DifficultyStep Step(SettingsFile settings, int difficulty)
    {
        if (Covering(settings, difficulty) is not { } league)
        {
            return new DifficultyStep(difficulty, string.Empty, difficulty, difficulty.ToString());
        }

        var step = difficulty - league.MinimumDifficulty;

        return new DifficultyStep(difficulty, league.Name, step, $"{league.Name} +{step}");
    }

    private static LeagueEntry? Covering(SettingsFile settings, int difficulty)
    {
        LeagueEntry? found = null;

        foreach (var league in settings.Leagues)
        {
            if (league.Kind == LeagueKind.Pinnacle) continue;
            if (league.MinimumDifficulty > difficulty) continue;
            if (found is not null && found.MinimumDifficulty >= league.MinimumDifficulty) continue;

            found = league;
        }

        return found;
    }

    public static double? HealthScale(SettingsFile settings, string? scaling, int difficulty) =>
        Scale(settings, scaling, difficulty, table => table.Health);

    public static double? DamageScale(SettingsFile settings, string? scaling, int difficulty) =>
        Scale(settings, scaling, difficulty, table => table.Damage);

    private static double? Scale(
        SettingsFile settings,
        string? scaling,
        int difficulty,
        Func<DifficultyScaling, IReadOnlyDictionary<int, double>> ladder)
    {
        if (scaling is { Length: > 0 } named &&
            settings.DifficultyScaling.Namespaces.TryGetValue(named, out var byNamespace) &&
            ladder(byNamespace).TryGetValue(difficulty, out var own))
        {
            return own;
        }

        return ladder(settings.DifficultyScaling).TryGetValue(difficulty, out var shared) ? shared : null;
    }

    public static double? Health(CreatureDocument creature, SettingsFile settings, int difficulty) =>
        creature.Health is { } health && HealthScale(settings, creature.Scaling, difficulty) is { } scale
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
        if (DamageScale(settings, owner.Scaling, difficulty) is not { } scale) return null;

        var mean = (decimal)attribute * (decimal)coefficient.Value * (decimal)scale;
        var half = (decimal)(coefficient.Spread ?? 0) / 2m;

        return new DamageBand(mean, mean * (1m - half), mean * (1m + half));
    }
}
