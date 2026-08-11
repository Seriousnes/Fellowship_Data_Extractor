using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record StatLine(string? Stat, string DisplayName, decimal Value, string Kind, bool Rolled)
{
    public bool Guaranteed => Kind != SlotKinds.Random;

    public bool Unresolved => Stat is null;
}

public static class StatMagnitudes
{
    private const string StaminaStat = "Stamina";

    private const string ArmorStat = "Armor";

    private const string RandomStatSlot = "RandomStat";

    private const string RandomStatLabel = "[Random Stat]";

    private const decimal NativeFactor = 1.08m;

    private const decimal StaminaRollShare = 0.375m;

    private const decimal PrimaryRollShare = 0.125m;

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
            ? (decimal)weights.StatWeight
            : 0m;

        var primaryPool = Pool(calculation.PrimaryPool, itemLevel) * slotWeight;
        var secondaryPool = Pool(calculation.SecondaryPool, itemLevel) * slotWeight;

        var lines = new List<StatLine>();
        var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (armor is { } worth) lines.Add(worth);

        var budget = (item.Stats ?? []).Sum(slot => slot.Weight);

        foreach (var declared in item.Stats ?? [])
        {
            var at = taken.GetValueOrDefault(declared.Stat);
            taken[declared.Stat] = at + 1;

            var named = query.Stats.TryGetValue(declared.Stat, out var values);
            var sent = named ? at < values!.Count ? values[at] : 0m : (decimal?)null;

            var onPrimaryPool = OnPrimaryPool(declared.Stat, primaryAttributes);
            var settings = declared.Kind == SlotKinds.Primary ? calculation.BaseSlot : calculation.FixedSlot;

            var share = onPrimaryPool && budget > 0
                ? (decimal)declared.Weight / budget
                : declared.Weight / 100m;

            var value = sent
                ?? (onPrimaryPool ? primaryPool : secondaryPool)
                    * share
                    * Multiplier(settings, calculation.FixedSlot, declared.Stat, onPrimaryPool, primaryAttributes)
                    * NativeFactor;

            lines.Add(new StatLine(
                declared.Stat,
                Label(calculation, declared.Stat),
                Round(value, calculation, declared.Stat),
                declared.Kind,
                Rolled: false));
        }

        var spare = Spare(item, query, taken);

        foreach (var stat in query.RandomStats)
        {
            spare.Add((stat, Roll(calculation, stat, primaryAttributes, primaryPool, secondaryPool)));
        }

        var spareAt = 0;

        for (var index = 0; index < Math.Max(rolledSlots, spare.Count); index++)
        {
            if (spareAt < spare.Count)
            {
                var (stat, value) = spare[spareAt++];
                lines.Add(new StatLine(
                    stat, Label(calculation, stat), Round(value, calculation, stat),
                    SlotKinds.Random, Rolled: true));
                continue;
            }

            lines.Add(new StatLine(
                null,
                RandomStatLabel,
                Math.Round(Expected(calculation, primaryPool, secondaryPool), MidpointRounding.AwayFromZero),
                SlotKinds.Random,
                Rolled: true));
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

    private static int RandomSlots(ItemDocument item, StatCalculationFile calculation, RarityEntry? rarity)
    {
        if (rarity is null || item.RuleSet is not { } ruleSet) return 0;
        if (!calculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;
        if (!byTier.TryGetValue(rarity.Tier, out var added)) return 0;

        return added.GuaranteedSlots.Count(slot =>
            slot.Equals(RandomStatSlot, StringComparison.OrdinalIgnoreCase));
    }

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

    private static decimal Roll(
        StatCalculationFile calculation,
        string stat,
        IReadOnlySet<string> primaryAttributes,
        decimal primaryPool,
        decimal secondaryPool)
    {
        var settings = calculation.DynamicSlot;

        var onPrimaryPool = OnPrimaryPool(stat, primaryAttributes);

        var share = stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase) ? StaminaRollShare
            : onPrimaryPool ? PrimaryRollShare
            : (decimal)(settings.SecondaryWeight ?? 0);

        return (onPrimaryPool ? primaryPool : secondaryPool)
            * share
            * Multiplier(settings, calculation.FixedSlot, stat, onPrimaryPool, primaryAttributes)
            * NativeFactor;
    }

    private static decimal Expected(StatCalculationFile calculation, decimal primaryPool, decimal secondaryPool)
    {
        var settings = calculation.DynamicSlot;

        var stamina = (decimal)(settings.StaminaWeight ?? 0);
        var secondary = (decimal)(settings.SecondaryWeight ?? 0);
        var primary = Math.Max(0m, 1m - stamina - secondary);

        var expected =
            stamina * primaryPool * (decimal)(settings.StaminaMultiplierAtThreshold ?? 1) +
            secondary * secondaryPool * (decimal)(settings.SecondaryMultiplier ?? 1) +
            primary * primaryPool * (decimal)(settings.PrimaryMultiplier ?? 1);

        return expected * (decimal)(settings.WeightMultiplier ?? 0) * NativeFactor;
    }

    private static bool OnPrimaryPool(string stat, IReadOnlySet<string> primaryAttributes) =>
        stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase) || primaryAttributes.Contains(stat);

    private static decimal Multiplier(
        SlotStatSettings settings,
        SlotStatSettings fallback,
        string stat,
        bool onPrimaryPool,
        IReadOnlySet<string> primaryAttributes)
    {
        if (stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase))
        {
            return Shipped(settings.StaminaMultiplierAtThreshold, fallback.StaminaMultiplierAtThreshold);
        }

        if (onPrimaryPool && primaryAttributes.Contains(stat))
        {
            return Shipped(settings.PrimaryMultiplier, fallback.PrimaryMultiplier);
        }

        return Shipped(settings.SecondaryMultiplier, fallback.SecondaryMultiplier);
    }

    private static decimal Shipped(double? own, double? fallback) => (decimal)(own ?? fallback ?? 1);

    private static decimal Pool(PrimaryStatPool? pool, int itemLevel) =>
        pool is null || itemLevel < pool.MinimumItemLevel
            ? 0m
            : (decimal)(pool.Multiplier
                * Math.Pow(pool.GrowthBase, itemLevel / pool.LevelDivisor)
                * Math.Sqrt(itemLevel));

    private static decimal Pool(SecondaryStatPool? pool, int itemLevel) =>
        pool is null || itemLevel < pool.MinimumItemLevel
            ? 0m
            : (decimal)(pool.Multiplier * Math.Sqrt(itemLevel * pool.LevelMultiplier));

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
