using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record ItemPowerInputs(
    string Slot,
    int Level,
    int MaxUpgrades,
    int RerollableSlots,
    bool Legendary,
    bool HasGemSocket,
    bool HasGrantedAbility,
    bool HasTraitTree,
    bool HasSetBonus);

public sealed record ItemPowerTerm(string Label, int? Count, double Factor, double? Weight, float Amount);

public sealed record OnceScore(string Primary, string? Qualifier, double Factor, Func<ItemPowerInputs, bool> Fires)
{
    public string Label => Qualifier is null ? Primary : $"{Primary} {Qualifier}";
}

public static class ItemPower
{
    public const string WeaponType = "Weapon";
    public const string NecklaceType = "Necklace";
    public const string TrinketType = "Trinket";

    public static string? Printed(ItemSettingsFile items, string type) =>
        items.SlotNames.GetValueOrDefault(type);

    private static bool Wears(ItemSettingsFile items, string? slot, string type) =>
        slot is not null && Printed(items, type) is { } printed &&
        printed.Equals(slot, StringComparison.OrdinalIgnoreCase);

    public static ItemPowerInputs? Inputs(
        ItemDocument item, SettingsFile settings, RarityEntry? rarity, TooltipQuery query, int? level)
    {
        if (item.Slot is not { } slot) return null;
        if (level is not { } itemLevel) return null;
        if (!settings.StatCalculation.SlotWeights.ContainsKey(slot)) return null;

        var slots = ItemCraft.Slots(item, settings, rarity);
        if (slots.Count == 0 && rarity is null) return null;

        bool Carries(string name) =>
            slots.Any(craft => craft.SlotName.Equals(name, StringComparison.OrdinalIgnoreCase));

        return new ItemPowerInputs(
            Slot: slot,
            Level: itemLevel,
            MaxUpgrades: query.MaxUpgrades
                ?? Tempering.Ceiling(settings.MaxTempers, rarity?.Name, itemLevel)
                ?? 0,
            RerollableSlots: slots.Count(
                craft => craft.SlotName.Equals(CraftSlots.RandomRolls, StringComparison.OrdinalIgnoreCase)),
            Legendary: rarity is not null && item.Rarity is Rarity.Legendary,
            HasGemSocket: !query.NoSocket && Carries(CraftSlots.GemSocket),
            HasGrantedAbility: Carries(CraftSlots.GrantedAbility),
            HasTraitTree: Carries(CraftSlots.TraitTree),
            HasSetBonus: Carries(CraftSlots.SetBonus));
    }

    public static IReadOnlyList<ItemPowerTerm> Terms(ItemPowerInputs given, SettingsFile settings)
    {
        var factors = settings.Items.PowerPotential;
        var weight = settings.StatCalculation.SlotWeights[given.Slot].StatWeight;

        var terms = new List<ItemPowerTerm>
        {
            new("Item level", given.Level, factors.ItemLevelScoreFactor, weight,
                given.Level * (float)factors.ItemLevelScoreFactor * (float)weight),
            new("Tempering ceiling", given.MaxUpgrades, factors.TemperScoreFactor, null,
                given.MaxUpgrades * (float)factors.TemperScoreFactor),
            new("Rerollable slots", given.RerollableSlots, factors.ModSlotScoreFactor, null,
                given.RerollableSlots * (float)factors.ModSlotScoreFactor),
        };

        foreach (var once in OnceScores(settings))
        {
            if (once.Fires(given))
            {
                terms.Add(new ItemPowerTerm(once.Label, null, once.Factor, null, (float)once.Factor));
            }
        }

        return terms;
    }

    public static IReadOnlyList<OnceScore> OnceScores(SettingsFile settings)
    {
        var items = settings.Items;
        var factors = items.PowerPotential;

        string Slot(string type) => Printed(items, type) ?? type;
        bool Wearing(ItemPowerInputs given, string type) => Wears(items, given.Slot, type);

        return
        [
            new("Gem socket", null, factors.GemSocketScoreFactor,
                given => given.HasGemSocket),
            new("Set bonus", null, factors.SetBonusScoreFactor,
                given => given.HasSetBonus),
            new(Slot(WeaponType), "ability", factors.WeaponAbilityScoreFactor,
                given => given.HasGrantedAbility && Wearing(given, WeaponType)),
            new(Slot(TrinketType), "ability", factors.RelicAbilityScoreFactor,
                given => given.HasGrantedAbility && Wearing(given, TrinketType)),
            new(Slot(WeaponType), "trait tree", factors.WeaponTraitTreeScoreFactor,
                given => given.HasTraitTree && Wearing(given, WeaponType)),
            new(Slot(NecklaceType), "trait tree", factors.NecklaceTraitTreeScoreFactor,
                given => given.HasTraitTree && Wearing(given, NecklaceType)),
            new("Legendary", "rung", factors.LegendaryScoreFactor,
                given => given.Legendary),
        ];
    }

    public static float Potential(ItemPowerInputs given, SettingsFile settings)
    {
        var score = 0f;

        foreach (var term in Terms(given, settings)) score += term.Amount;

        return score;
    }

    public static decimal? Of(
        ItemDocument item, SettingsFile settings, RarityEntry? rarity, TooltipQuery query, int? level) =>
        Inputs(item, settings, rarity, query, level) is { } given
            ? StatRows.RoundHalfToEven(Potential(given, settings))
            : null;
}
