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

    public static IReadOnlyList<ItemPowerTerm> Terms(ItemPowerInputs inputs, SettingsFile settings)
    {
        var factors = settings.Items.PowerPotential;
        var weight = settings.StatCalculation.SlotWeights[inputs.Slot].StatWeight;

        var terms = new List<ItemPowerTerm>
        {
            new("Item level", inputs.Level, factors.ItemLevelScoreFactor, weight,
                inputs.Level * (float)factors.ItemLevelScoreFactor * (float)weight),
            new("Tempering ceiling", inputs.MaxUpgrades, factors.TemperScoreFactor, null,
                inputs.MaxUpgrades * (float)factors.TemperScoreFactor),
            new("Rerollable slots", inputs.RerollableSlots, factors.ModSlotScoreFactor, null,
                inputs.RerollableSlots * (float)factors.ModSlotScoreFactor),
        };

        foreach (var once in OnceScores(settings))
        {
            if (once.Fires(inputs))
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
        bool Wearing(ItemPowerInputs inputs, string type) => Wears(items, inputs.Slot, type);

        return
        [
            new("Gem socket", null, factors.GemSocketScoreFactor,
                inputs => inputs.HasGemSocket),
            new("Set bonus", null, factors.SetBonusScoreFactor,
                inputs => inputs.HasSetBonus),
            new(Slot(WeaponType), "ability", factors.WeaponAbilityScoreFactor,
                inputs => inputs.HasGrantedAbility && Wearing(inputs, WeaponType)),
            new(Slot(TrinketType), "ability", factors.RelicAbilityScoreFactor,
                inputs => inputs.HasGrantedAbility && Wearing(inputs, TrinketType)),
            new(Slot(WeaponType), "trait tree", factors.WeaponTraitTreeScoreFactor,
                inputs => inputs.HasTraitTree && Wearing(inputs, WeaponType)),
            new(Slot(NecklaceType), "trait tree", factors.NecklaceTraitTreeScoreFactor,
                inputs => inputs.HasTraitTree && Wearing(inputs, NecklaceType)),
            new("Legendary", "rung", factors.LegendaryScoreFactor,
                inputs => inputs.Legendary),
        ];
    }

    public static float Potential(ItemPowerInputs inputs, SettingsFile settings)
    {
        var score = 0f;

        foreach (var term in Terms(inputs, settings)) score += term.Amount;

        return score;
    }

    public static decimal? Of(
        ItemDocument item, SettingsFile settings, RarityEntry? rarity, TooltipQuery query, int? level) =>
        Inputs(item, settings, rarity, query, level) is { } inputs
            ? StatRows.RoundHalfToEven(Potential(inputs, settings))
            : null;
}
