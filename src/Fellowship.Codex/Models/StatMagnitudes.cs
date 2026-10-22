using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record StatLine(string? Stat, string DisplayName, decimal Value, string Kind, bool Rolled)
{
    public bool Guaranteed => Kind != SlotKinds.Random;

    public bool Unresolved => Stat is null;
}

public static class StatMagnitudes
{
    private const string StaminaStat = "Stamina";

    private const string ArmorStat = "Armor";

    public static IReadOnlyList<StatLine> For(
        ItemDocument item,
        StatCalculationFile calculation,
        IReadOnlySet<string> primaryAttributes,
        RarityEntry? rarity,
        int itemLevel,
        TooltipQuery query)
    {
        var armor = (query.Armor ?? Armor(item, calculation, itemLevel)) is { } worn
            ? new StatLine(ArmorStat, Label(calculation, ArmorStat), worn, SlotKinds.Fixed, Rolled: false)
            : null;

        if (item.Stats is not { Count: > 0 } && query.Stats.Count == 0 && armor is null) return [];

        var slotWeight = item.Slot is { } slot && calculation.SlotWeights.TryGetValue(slot, out var weights)
            ? (float)weights.StatWeight
            : 0f;

        var primaryPool = StatRows.PrimaryPool(calculation, itemLevel);
        var secondaryPool = StatRows.SecondaryPool(itemLevel);

        var declaredWeights = StatRows.Weights([.. (item.Stats ?? []).Select(slot => slot.Weight)]);
        var guaranteedBlock = StatRows.Guaranteed(calculation);
        var tempers = Math.Max(0, query.Upgrades ?? 0);
        var ordinal = 0;

        decimal Row(string stat)
        {
            var onPrimaryPool = OnPrimaryPool(stat, primaryAttributes);
            var stamina = stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase);

            var magnitude = StatRows.Magnitude(
                onPrimaryPool ? primaryPool : secondaryPool,
                slotWeight,
                StatRows.BlockTerm(guaranteedBlock, stamina, onPrimaryPool, itemLevel),
                StatRows.Share(declaredWeights, ordinal++),
                tempers,
                guaranteed: true);

            return StatRows.RoundHalfToEven(magnitude);
        }

        var lines = new List<StatLine>();
        var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (armor is { } worth) lines.Add(worth);

        foreach (var declared in item.Stats ?? [])
        {
            var at = taken.GetValueOrDefault(declared.Stat);
            taken[declared.Stat] = at + 1;

            var named = query.Stats.TryGetValue(declared.Stat, out var values);
            var sent = named ? at < values!.Count ? values[at] : 0m : (decimal?)null;

            var value = sent is { } amount
                ? Round(amount, calculation, declared.Stat)
                : Row(declared.Stat);

            lines.Add(new StatLine(
                declared.Stat, Label(calculation, declared.Stat), value, declared.Kind, Rolled: false));
        }

        foreach (var (stat, value) in Spare(item, query, taken))
        {
            lines.Add(new StatLine(
                stat, Label(calculation, stat), Round(value, calculation, stat), SlotKinds.Random, Rolled: true));
        }

        return Fold(lines);
    }

    public static decimal? Armor(ItemDocument item, StatCalculationFile calculation, int itemLevel)
    {
        if (item.Slot is not { } slot || !calculation.SlotWeights.TryGetValue(slot, out var worn)) return null;
        if (worn.ArmorWeight <= 0) return null;

        if (item.ArmorType?.ToString() is not { } armorType ||
            !calculation.ArmorWeights.TryGetValue(armorType, out var kind)) return null;

        return Math.Ceiling(
            (decimal)worn.ArmorWeight * (itemLevel * (decimal)kind.ArmorWeight + (decimal)kind.FlatArmor));
    }

    private static List<StatLine> Fold(List<StatLine> lines)
    {
        var folded = new List<StatLine>(lines.Count);
        var at = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (line.Stat is not { } stat) { folded.Add(line); continue; }

            if (at.TryGetValue(stat, out var index))
            {
                var running = folded[index];

                folded[index] = running with
                {
                    Value = running.Value + line.Value,
                    Kind = running.Kind == SlotKinds.Primary || line.Kind == SlotKinds.Primary
                        ? SlotKinds.Primary
                        : running.Kind,
                    Rolled = running.Rolled || line.Rolled,
                };

                continue;
            }

            at[stat] = folded.Count;
            folded.Add(line);
        }

        return folded;
    }

    private static bool OnPrimaryPool(string stat, IReadOnlySet<string> primaryAttributes) =>
        stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase) || primaryAttributes.Contains(stat);

    private static List<(string Stat, decimal Value)> Spare(
        ItemDocument item, TooltipQuery query, IReadOnlyDictionary<string, int> taken)
    {
        var spare = new List<(string, decimal)>();

        foreach (var (stat, values) in query.Stats)
        {
            for (var at = taken.GetValueOrDefault(stat); at < values.Count; at++) spare.Add((stat, values[at]));
        }

        return spare;
    }

    private static string Label(StatCalculationFile calculation, string stat) =>
        calculation.Attributes.TryGetValue(stat, out var attribute) ? attribute.DisplayName : stat;

    private static decimal Round(decimal value, StatCalculationFile calculation, string? stat) =>
        Math.Round(
            value,
            stat is not null && calculation.Attributes.TryGetValue(stat, out var attribute)
                ? attribute.RoundToDecimals
                : 0,
            MidpointRounding.AwayFromZero);
}
