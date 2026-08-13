using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record StatLine(string? Stat, string DisplayName, decimal Value, string Kind, bool Rolled)
{
    private static readonly int[] None = [];

    public IReadOnlyList<int> Rolls { get; init; } = None;

    public bool Guaranteed => Kind != SlotKinds.Random;

    public bool Unresolved => Stat is null;
}

public static class StatMagnitudes
{
    private const string StaminaStat = "Stamina";

    private const string ArmorStat = "Armor";

    private const string RandomStatSlot = "RandomStat";

    private const string RandomStatLabel = "[Random Stat]";

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

        var rolledSlots = RandomSlots(item, calculation, rarity);
        if (item.Stats is not { Count: > 0 } && rolledSlots == 0 && armor is null) return [];

        var slotWeight = item.Slot is { } slot && calculation.SlotWeights.TryGetValue(slot, out var weights)
            ? (float)weights.StatWeight
            : 0f;

        var primaryPool = StatRows.PrimaryPool(calculation, itemLevel);
        var secondaryPool = StatRows.SecondaryPool(itemLevel);

        var declaredWeights = StatRows.Weights([.. (item.Stats ?? []).Select(slot => slot.Weight)]);
        var guaranteedBlock = StatRows.Guaranteed(calculation);
        var rolledBlock = StatRows.Rolled(calculation);
        var tempers = Math.Max(0, query.Upgrades ?? 0);
        var ordinal = 0;

        decimal Row(string stat, bool guaranteed)
        {
            var onPrimaryPool = OnPrimaryPool(stat, primaryAttributes);
            var stamina = stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase);

            var magnitude = StatRows.Magnitude(
                onPrimaryPool ? primaryPool : secondaryPool,
                slotWeight,
                StatRows.BlockTerm(guaranteed ? guaranteedBlock : rolledBlock, stamina, onPrimaryPool, itemLevel),
                StatRows.Share(declaredWeights, ordinal++),
                tempers,
                guaranteed);

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

            var value = sent is { } given
                ? Round(given, calculation, declared.Stat)
                : Row(declared.Stat, guaranteed: true);

            lines.Add(new StatLine(
                declared.Stat, Label(calculation, declared.Stat), value, declared.Kind, Rolled: false));
        }

        var spare = Spare(item, query, taken);
        var drawn = Math.Max(rolledSlots, spare.Count + query.RandomStats.Count);

        for (var index = 0; index < drawn; index++)
        {
            if (index < spare.Count)
            {
                var (stat, value) = spare[index];
                lines.Add(new StatLine(
                    stat, Label(calculation, stat), Round(value, calculation, stat),
                    SlotKinds.Random, Rolled: true));
                ordinal++;
                continue;
            }

            var roll = index - spare.Count;

            if (roll < query.RandomStats.Count)
            {
                var stat = query.RandomStats[roll];
                lines.Add(new StatLine(
                    stat, Label(calculation, stat), Row(stat, guaranteed: false), SlotKinds.Random, Rolled: true)
                {
                    Rolls = [roll],
                });
                continue;
            }

            lines.Add(new StatLine(
                null,
                RandomStatLabel,
                StatRows.RoundHalfToEven(StatRows.Magnitude(
                    secondaryPool,
                    slotWeight,
                    StatRows.BlockTerm(rolledBlock, stamina: false, onPrimaryPool: false, itemLevel),
                    StatRows.Share(declaredWeights, ordinal++),
                    tempers: 0,
                    guaranteed: false)),
                SlotKinds.Random,
                Rolled: true)
            {
                Rolls = [roll],
            });
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
                    Rolls = line.Rolls.Count == 0 ? running.Rolls : [.. running.Rolls, .. line.Rolls],
                };

                continue;
            }

            at[stat] = folded.Count;
            folded.Add(line);
        }

        return folded;
    }

    private static int RandomSlots(ItemDocument item, StatCalculationFile calculation, RarityEntry? rarity)
    {
        if (rarity is null || item.RuleSet is not { } ruleSet) return 0;
        if (!calculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;
        if (!byTier.TryGetValue(rarity.Tier, out var added)) return 0;

        return added.GuaranteedSlots.Count(slot =>
            slot.Equals(RandomStatSlot, StringComparison.OrdinalIgnoreCase));
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
