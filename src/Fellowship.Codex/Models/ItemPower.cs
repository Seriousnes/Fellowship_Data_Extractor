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
        var items = settings.Items;
        var factors = items.PowerPotential;
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

        void Flat(bool fires, string label, double factor)
        {
            if (fires) terms.Add(new ItemPowerTerm(label, null, factor, null, (float)factor));
        }

        string Named(string type, string what) => $"{Printed(items, type) ?? type} {what}";

        Flat(given.HasGemSocket, "Gem socket", factors.GemSocketScoreFactor);

        Flat(given.HasGrantedAbility && Wears(items, given.Slot, WeaponType),
            Named(WeaponType, "ability"), factors.WeaponAbilityScoreFactor);

        Flat(given.HasGrantedAbility && Wears(items, given.Slot, TrinketType),
            Named(TrinketType, "ability"), factors.RelicAbilityScoreFactor);

        Flat(given.HasTraitTree && Wears(items, given.Slot, WeaponType),
            Named(WeaponType, "trait tree"), factors.WeaponTraitTreeScoreFactor);

        Flat(given.HasTraitTree && Wears(items, given.Slot, NecklaceType),
            Named(NecklaceType, "trait tree"), factors.NecklaceTraitTreeScoreFactor);

        Flat(given.HasSetBonus, "Set bonus", factors.SetBonusScoreFactor);

        Flat(given.Legendary, "Legendary", factors.LegendaryScoreFactor);

        return terms;
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
