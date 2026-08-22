namespace Fellowship.SDK;

public sealed record HeroEntry(string Name, string? ArmorType, string PrimaryStat)
{
    public string? Color { get; init; }
}

public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);

public sealed record AbilityCategoryEntry(string Name, int Value);

public sealed record TemperCeiling(int ItemLevel, int MaxTempers);

public sealed record DropPlateau(string Row, int Difficulty, int ItemLevel);

public sealed record BlessingEntry(string Name, string HeroBlessing, string TierLevel, int Tiers);

public sealed record DifficultyScaling
{
    public IReadOnlyDictionary<int, double> Health { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double> Damage { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<string, DifficultyScaling> Namespaces { get; init; } =
        new Dictionary<string, DifficultyScaling>(StringComparer.OrdinalIgnoreCase);
}

[System.Text.Json.Serialization.JsonConverter(
    typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LeagueKind>))]
public enum LeagueKind
{
    Ranked = 1,
    Eternal = 2,
    Pinnacle = 3,
}

public sealed record LeagueEntry(string Row, string Name, string Tag, LeagueKind Kind)
{
    public int MinimumDifficulty { get; init; }

    public int MaximumDifficulty { get; init; }

    public IReadOnlyList<int> Difficulties { get; init; } = [];
}
