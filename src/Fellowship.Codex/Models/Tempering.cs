namespace Fellowship.Codex.Models;

public static class Tempering
{
    public static int? Ceiling(
        IReadOnlyDictionary<string, IReadOnlyList<TemperCeiling>> ladders, string? rarity, int? itemLevel)
    {
        if (rarity is null || itemLevel is not { } level) return null;
        if (!ladders.TryGetValue(rarity, out var ladder) || ladder.Count == 0) return null;

        var ceiling = ladder[0].MaxTempers;

        foreach (var step in ladder)
        {
            if (step.ItemLevel > level) break;

            ceiling = step.MaxTempers;
        }

        return ceiling;
    }
}
