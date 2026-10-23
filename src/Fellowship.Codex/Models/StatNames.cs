namespace Fellowship.Codex.Models;

public static class StatNames
{
    public static IReadOnlyDictionary<string, string> Shortened { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Stamina"] = "Stam",
        ["Strength"] = "Str",
        ["Intellect"] = "Int",
        ["Agility"] = "Agi",
        ["CritChance"] = "Crit",
        ["Expertise"] = "Exp",
    };

    public static string Short(string? key, string displayName) =>
        key is not null && Shortened.TryGetValue(key, out var shortened) ? shortened : displayName;
}
