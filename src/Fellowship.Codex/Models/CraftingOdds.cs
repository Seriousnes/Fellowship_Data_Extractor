using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record CraftingCandidate(EntityDocument Document, double Weight);

public sealed record CraftingAttempt(CraftingRecipeEntry Recipe, ModifierRecipe Reroll, double Chance)
{
    public double ExpectedCrafts => 1 / Chance;
}

public static class CraftingOdds
{
    public static IReadOnlyList<string> GearSlots(ModifierKind kind, IEntityStoreService store)
    {
        var additions = store.Settings.StatCalculation.RarityAdditions;

        return
        [
            .. store.Entities
                .OfType<ItemDocument>()
                .Where(item => item is { Slot: not null, RuleSet: not null })
                .Where(item => additions.TryGetValue(item.RuleSet!, out var byTier) &&
                               byTier.Values.Any(tier => tier.RandomizedSlots.Contains(kind.ToString(), StringComparer.Ordinal)))
                .Select(item => item.Slot!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(slot => Pool(kind, slot, store).Count > 0)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    public static IReadOnlyList<CraftingCandidate> Pool(
        ModifierKind kind, string gearSlot, IEntityStoreService store, string? wearer = null) =>
    [
        .. ItemCraft.Pool(kind.ToString(), store, wearer)
            .DistinctBy(document => document.Id)
            .Where(document => document is not GlyphDocument glyph ||
                               (glyph.Slots ?? []).Contains(gearSlot, StringComparer.OrdinalIgnoreCase))
            .Select(document => new CraftingCandidate(document, Weight(document)))
            .Where(candidate => candidate.Weight > 0)
            .OrderBy(candidate => PublicTypes.NameOf(candidate.Document), StringComparer.Ordinal),
    ];

    public static IReadOnlyList<CraftingAttempt> Attempts(
        ModifierKind kind, int modifierSlot, IReadOnlyList<CraftingCandidate> pool, int target, SettingsFile settings)
    {
        var weights = pool.Select(candidate => candidate.Weight).ToArray();

        return
        [
            .. TooltipView.Type(kind, settings).Recipes
                .Where(reroll => reroll.SlotIndex == modifierSlot)
                .OrderBy(reroll => reroll.AllOptions)
                .ThenBy(reroll => reroll.Options)
                .Select(reroll => new CraftingAttempt(Recipe(reroll.Recipe, settings), reroll, Chance(weights, target, reroll))),
        ];
    }

    public static double Chance(IReadOnlyList<double> weights, int target, ModifierRecipe reroll) =>
        reroll.AllOptions ? 1 : Offered(weights, target, Math.Max(reroll.Options, 1), ItemCraft.SameIdentity);

    public static double Offered(IReadOnlyList<double> weights, int target, int offers, int cap) =>
        1 - Absent(weights, target, new int[weights.Count], offers, cap);

    public static CraftingRecipeEntry Recipe(string id, SettingsFile settings) =>
        settings.CraftingRecipes.FirstOrDefault(recipe => recipe.Recipe == id)
        ?? throw new InvalidOperationException($"The settings state no crafting recipe {id}.");

    private static double Weight(EntityDocument document) =>
        document is AugmentationDocument augmentation
            ? augmentation.RandomChance ?? 0
            : 1;

    private static double Absent(IReadOnlyList<double> weights, int target, int[] drawn, int remaining, int cap)
    {
        if (remaining == 0) return 1;

        var open = 0d;

        for (var at = 0; at < weights.Count; at++)
        {
            if (drawn[at] < cap) open += weights[at];
        }

        if (open == 0) return 1;

        var absent = 0d;

        for (var at = 0; at < weights.Count; at++)
        {
            if (at == target || drawn[at] >= cap) continue;

            drawn[at]++;
            absent += weights[at] / open * Absent(weights, target, drawn, remaining - 1, cap);
            drawn[at]--;
        }

        return absent;
    }
}
