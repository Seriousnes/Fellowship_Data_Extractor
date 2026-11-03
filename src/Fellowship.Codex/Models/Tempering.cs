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

    public static float StaminaMultiplier(ItemSettingsFile items, int itemLevel)
    {
        var multiplier = (float)items.TemperDefaultStaminaMultiplier;

        foreach (var step in items.TemperStaminaMultiplierAtItemLevel)
        {
            if (step.ItemLevel > itemLevel) break;

            multiplier = (float)step.TemperValue;
        }

        return multiplier;
    }
}
