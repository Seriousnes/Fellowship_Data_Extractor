using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// One line of an item's stat block: the stat, the words the game prints for it, and how much of
/// it this copy of the item carries. A slot whose stat is rolled when the item drops has no stat
/// name of its own and prints as a random one.
/// </summary>
public sealed record StatLine(string? Stat, string DisplayName, decimal Value, string Kind, bool OnPrimaryPool)
{
    /// <summary>Set on the slot the wearer's own attribute fills.</summary>
    public bool Primary => Kind == SlotKinds.Primary;

    /// <summary>Set on a slot whose stat the item rolls and the caller has not said what it rolled.</summary>
    public bool Unresolved => Stat is null;
}

/// <summary>
/// What an item's stats come out at, worked out from its level the way the build works it out.
///
/// An item declares which stats it has and the share of its budget each slot is worth, never how
/// much of them: the magnitude is the item's level run through the two shipped pool curves, cut
/// down to the equipment position the item is worn in, split by the item's own weights, and scaled
/// by whichever block of the calculation settings the slot's kind belongs to. Every one of those
/// numbers is in the export, so none of them is restated here.
///
/// Two pools, not one. Stamina and the wearer's attribute draw on the primary pool and the four
/// secondary attributes on the secondary pool, which is what the settings mean by naming their
/// first knob PrimaryAndStaminaMultiplier. The build confirms it: a Regal item level 315 pair of
/// legs reads 50 Stamina against 14 Intellect, and only Stamina on the primary pool at the
/// stamina multiplier reproduces that ratio from the item's shipped weights of 40 and 17.
///
/// Rarity adds slots and does not scale them. The settings ship StatRarityBase at one, so a rung
/// changes an item's stats only by granting it more of them.
/// </summary>
public static class StatMagnitudes
{
    /// <summary>
    /// The item level a request that names none is answered at.
    ///
    /// Read from the build: this is where the Default row of CT_DungeonDifficultyToDropItemLevel
    /// plateaus, at dungeon difficulty 21, and no non-legendary drop passes it. The Legendary row
    /// of that same curve plateaus at 360, so a legendary answered here is answered 45 levels
    /// below the highest it drops at.
    /// </summary>
    public const int MaximumItemLevel = 315;

    private const string StaminaStat = "Stamina";

    /// <summary>The slot name a rarity grants that carries a rolled stat.</summary>
    private const string RandomStatSlot = "RandomStat";

    private const string RandomStatLabel = "[Random Stat]";

    /// <summary>
    /// Every stat line one copy of an item shows, in the item's own slot order and with the slots
    /// its rarity grants after them.
    ///
    /// A caller's own rolls win over the computed magnitude, one value per slot of that stat in
    /// the order they arrived, because a caller sending numbers is describing the copy in their
    /// hands rather than asking what a copy would come out at. Anything they send beyond the
    /// item's declared slots fills the rolled ones, so a fully described item shows no rolled
    /// slot at all.
    ///
    /// The order is the game's rather than the item's. An in-game tooltip for item 5309 lists
    /// Intellect above Stamina where the item declares Stamina first, so the wearer's own
    /// attribute is lifted to the top and everything else keeps the order the item states.
    ///
    /// A stat is one line however many slots hold it. The Ring of Boomtastic Explosions rolls two
    /// haste slots and reads +14 Haste, not +7 twice: slots are how the budget is divided and a
    /// player reads the total. A rolled slot the caller has named a stat for folds into that stat's
    /// line the same way, so naming haste on that ring raises the line rather than adding one. The
    /// rolled slots still unnamed stay one line each, because two unknown stats are not one stat.
    /// </summary>
    public static IReadOnlyList<StatLine> For(
        Entity entity,
        StatCalculationFile calculation,
        IReadOnlySet<string> primaryAttributes,
        RarityEntry? rarity,
        int itemLevel,
        TooltipQuery query)
    {
        var rolledSlots = RandomSlots(entity, calculation, rarity);
        if (entity.Stats is not { Count: > 0 } && rolledSlots == 0) return [];

        var slotWeight = entity.Slot is { } slot && calculation.SlotWeights.TryGetValue(slot, out var weights)
            ? (decimal)weights.StatWeight
            : 0m;

        var primaryPool = Pool(calculation.PrimaryPool, itemLevel) * slotWeight;
        var secondaryPool = Pool(calculation.SecondaryPool, itemLevel) * slotWeight;

        var lines = new List<StatLine>();
        var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // The wearer's own attribute is drawn first. OrderBy is stable, so every other slot keeps
        // the order the item declares it in.
        foreach (var declared in (entity.Stats ?? []).OrderBy(slot => slot.Kind == SlotKinds.Primary ? 0 : 1))
        {
            var at = taken.GetValueOrDefault(declared.Stat);
            taken[declared.Stat] = at + 1;

            // Naming a stat takes over every slot of it, not just the ones a value arrived for. A
            // caller who sends one haste against an item with two haste slots is describing what
            // their copy reads, so adding a computed second slot on top would answer a number they
            // never had.
            var named = query.Stats.TryGetValue(declared.Stat, out var values);
            var rolled = named ? at < values!.Count ? values[at] : 0m : (decimal?)null;

            var onPrimaryPool = OnPrimaryPool(declared.Stat, primaryAttributes);
            var settings = declared.Kind == SlotKinds.Primary ? calculation.BaseSlot : calculation.FixedSlot;

            var value = rolled
                ?? (onPrimaryPool ? primaryPool : secondaryPool)
                    * declared.Weight / 100m
                    * Multiplier(settings, declared.Stat, onPrimaryPool, primaryAttributes);

            lines.Add(new StatLine(
                declared.Stat, Label(calculation, declared.Stat), value, declared.Kind, onPrimaryPool));
        }

        // A rolled slot takes whatever the caller sent that no declared slot claimed, because a
        // caller listing five stats on an item that declares three is naming the two it rolled.
        var spare = Spare(entity, query, taken);
        var spareAt = 0;

        for (var index = 0; index < rolledSlots; index++)
        {
            if (spareAt < spare.Count)
            {
                var (stat, value) = spare[spareAt++];
                lines.Add(new StatLine(
                    stat, Label(calculation, stat), value, SlotKinds.Random,
                    OnPrimaryPool(stat, primaryAttributes)));
                continue;
            }

            lines.Add(new StatLine(
                null,
                RandomStatLabel,
                Rolled(calculation, primaryPool, secondaryPool),
                SlotKinds.Random,
                OnPrimaryPool: false));
        }

        // Rounded once the slots are added up, because the tooltip shows one number per stat and
        // that number is the total rounded, not the sum of separately rounded parts: two slots at
        // 6.6 are 13 on the item and would be 14 rounded apart.
        return [.. Fold(lines).Select(line => line with
        {
            Value = Round(line.Value, calculation, line.Stat),
        })];
    }

    /// <summary>
    /// One line per stat, holding what every slot of it adds up to, in the order the stats first
    /// appear. A line with no stat is a slot whose stat is still unknown and folds into nothing.
    ///
    /// A folded line keeps the wearer's-attribute marking if any slot in it carried one, because
    /// that is what the game draws the line in the wearer's colour for.
    /// </summary>
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
                };

                continue;
            }

            at[stat] = folded.Count;
            folded.Add(line);
        }

        return folded;
    }

    /// <summary>
    /// How many stats a rarity grants an item beyond the ones it declares. Only the guaranteed
    /// slots are counted: the rest are drawn at random from a pool that holds gem power and
    /// ability ranks as well as stats, so how many of them are stats differs per copy.
    /// </summary>
    private static int RandomSlots(Entity entity, StatCalculationFile calculation, RarityEntry? rarity)
    {
        if (rarity is null || entity.RuleSet is not { } ruleSet) return 0;
        if (!calculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;
        if (!byTier.TryGetValue(rarity.Tier, out var added)) return 0;

        return added.GuaranteedSlots.Count(slot =>
            slot.Equals(RandomStatSlot, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The caller's stat values that no declared slot consumed, in the order the stats were named.
    /// </summary>
    private static List<(string Stat, decimal Value)> Spare(
        Entity entity, TooltipQuery query, IReadOnlyDictionary<string, int> taken)
    {
        var spare = new List<(string, decimal)>();

        foreach (var (stat, values) in query.Stats)
        {
            for (var at = taken.GetValueOrDefault(stat); at < values.Count; at++) spare.Add((stat, values[at]));
        }

        return spare;
    }

    /// <summary>
    /// What a slot rolled at random is worth. Which stat it lands on is not known, so the
    /// magnitude is the average over the kinds it can land on, weighted by the shares the settings
    /// give them: stamina, a secondary attribute, and whatever is left over for a primary one.
    /// </summary>
    private static decimal Rolled(StatCalculationFile calculation, decimal primaryPool, decimal secondaryPool)
    {
        var settings = calculation.DynamicSlot;

        var stamina = (decimal)(settings.StaminaWeight ?? 0);
        var secondary = (decimal)(settings.SecondaryWeight ?? 0);
        var primary = Math.Max(0m, 1m - stamina - secondary);

        var expected =
            stamina * primaryPool * (decimal)(settings.StaminaMultiplierAtThreshold ?? 1) +
            secondary * secondaryPool * (decimal)(settings.SecondaryMultiplier ?? 1) +
            primary * primaryPool * (decimal)(settings.PrimaryMultiplier ?? 1);

        return expected * (decimal)(settings.WeightMultiplier ?? 0);
    }

    /// <summary>
    /// Which of the two pools a stat draws on. The wearer's attributes are the ones the hero index
    /// names, so the split comes from the export rather than from a list written here.
    /// </summary>
    private static bool OnPrimaryPool(string stat, IReadOnlySet<string> primaryAttributes) =>
        stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase) || primaryAttributes.Contains(stat);

    /// <summary>
    /// What the slot's kind does to its share, or one where the settings state nothing.
    ///
    /// The base slot's block carries only a stamina multiplier. The other fields are absent because
    /// a cooked package omits whatever matches the class default, not because the struct lacks
    /// them, so treating the absent primary multiplier as one is an inference rather than a reading.
    /// The in-game tooltip is what supports it: item 5309 reads 50 Stamina against 14 Intellect at
    /// shipped weights of 40 and 17, a ratio of 3.53 that only comes out with the stamina multiplier
    /// on one side and nothing on the other.
    /// </summary>
    private static decimal Multiplier(
        SlotStatSettings settings, string stat, bool onPrimaryPool, IReadOnlySet<string> primaryAttributes)
    {
        if (stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase))
        {
            return (decimal)(settings.StaminaMultiplierAtThreshold ?? 1);
        }

        if (onPrimaryPool && primaryAttributes.Contains(stat))
        {
            return (decimal)(settings.PrimaryMultiplier ?? 1);
        }

        return (decimal)(settings.SecondaryMultiplier ?? 1);
    }

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

    /// <summary>
    /// The words the game prints for a stat. CritChance reads "Critical Strike" on screen and the
    /// attribute table is what says so, so a renderer that printed the export's name would print
    /// a name no player has seen.
    /// </summary>
    private static string Label(StatCalculationFile calculation, string stat) =>
        calculation.Attributes.TryGetValue(stat, out var attribute) ? attribute.DisplayName : stat;

    /// <summary>
    /// A magnitude written the way the attribute table says to write it. A rolled slot has no
    /// attribute to ask, and every stat one can land on is a whole number, so it rounds the same
    /// way they do.
    /// </summary>
    private static decimal Round(decimal value, StatCalculationFile calculation, string? stat) =>
        Math.Round(
            value,
            stat is not null && calculation.Attributes.TryGetValue(stat, out var attribute)
                ? attribute.RoundToDecimals
                : 0,
            MidpointRounding.AwayFromZero);
}
