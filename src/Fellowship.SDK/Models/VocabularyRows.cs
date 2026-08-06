namespace Fellowship.SDK;

public sealed record HeroEntry(string Codename, string Name, string? ArmorType, string PrimaryStat)
{
    public string? Color { get; init; }
}

public sealed record RarityEntry(int Tier, string Name, string DisplayName, string GemDisplayName);

public sealed record TemperCeiling(int ItemLevel, int MaxTempers);

public sealed record DropPlateau(string Row, int Difficulty, int ItemLevel);
