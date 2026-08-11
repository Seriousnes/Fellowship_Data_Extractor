namespace Fellowship.SDK;

public sealed record HeroEntry(string Codename, string Name, string? ArmorType, string PrimaryStat)
{
    public string? Color { get; init; }
}

public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);

public sealed record TemperCeiling(int ItemLevel, int MaxTempers);

public sealed record DropPlateau(string Row, int Difficulty, int ItemLevel);

public sealed record BlessingEntry(string Name, string HeroBlessing, string TierLevel, int Tiers);

public sealed record DifficultyScaling
{
    public IReadOnlyDictionary<int, double> Health { get; init; } = new Dictionary<int, double>();

    public IReadOnlyDictionary<int, double> Damage { get; init; } = new Dictionary<int, double>();
}
