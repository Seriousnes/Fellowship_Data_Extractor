using System.Runtime.CompilerServices;
using Fellowship.Codex.Services;
using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class AugmentationRanks
{
    public static bool IsEnchant(EntityDocument document) => document is EnchantDocument;

    public static int Power(SettingsFile settings, int itemLevel) =>
        settings.Items.StatEnchantPower is { } curve
            ? (int)CurveReading.At(curve, itemLevel)
            : throw new InvalidOperationException(
                "The settings state no enchant power curve, so an enchant has no magnitude at any item level.");

    public static int RowValue(SettingsFile settings, int itemLevel) => Power(settings, itemLevel) - 1;

    public static AugmentationTooltipText Text(SettingsFile settings) =>
        settings.AugmentationTooltip
        ?? throw new InvalidOperationException("The settings state no augmentation tooltip text, so an augmentation card has no labels.");

    public static int PerSource(SettingsFile settings, AugmentationDocument augmentation) =>
        augmentation is EnchantDocument ? Power(settings, settings.Items.MaxItemLevel) : 1;

    public static IReadOnlyList<ItemTypeText> Carriers(IEntityStoreService store, AugmentationDocument augmentation) =>
        augmentation.CategoryTag is { } category
            ? CarriersByCategory.GetValue(store, Carried).GetValueOrDefault(category) ?? []
            : [];

    public static IReadOnlyList<ItemTypeText> Sources(
        IEntityStoreService store, TooltipQuery query, AugmentationDocument augmentation)
    {
        var carriers = Carriers(store, augmentation);

        if (query.RankSources.Count == 0) return carriers is [var first, ..] ? [first] : [];

        return
        [
            .. carriers.Where(carrier =>
                query.RankSources.Contains(carrier.Type, StringComparer.OrdinalIgnoreCase)),
        ];
    }

    public static int Summed(IEntityStoreService store, TooltipQuery query, AugmentationDocument augmentation) =>
        Sources(store, query, augmentation).Count * PerSource(store.Settings, augmentation);

    public static int Rank(IEntityStoreService store, TooltipQuery query, AugmentationDocument augmentation) =>
        Math.Min(query.EquippedTier ?? Summed(store, query, augmentation), augmentation.MaxRank ?? int.MaxValue);

    private static readonly ConditionalWeakTable<IEntityStoreService, IReadOnlyDictionary<string, IReadOnlyList<ItemTypeText>>>
        CarriersByCategory = new();

    private static IReadOnlyDictionary<string, IReadOnlyList<ItemTypeText>> Carried(IEntityStoreService store)
    {
        var settings = store.Settings;
        var categoriesBySlot = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in store.Entities.OfType<ItemDocument>())
        {
            if (item.Slot is not { } slot) continue;

            if (!categoriesBySlot.TryGetValue(slot, out var categories))
            {
                categoriesBySlot[slot] = categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (item.RuleSet is { } ruleSet && settings.StatCalculation.RarityAdditions.TryGetValue(ruleSet, out var tiers))
            {
                foreach (var name in tiers.Values.SelectMany(tier => tier.GuaranteedSlots.Concat(tier.RandomizedSlots)))
                {
                    if (settings.StatCalculation.SlotDefinitions.TryGetValue(name, out var definition))
                    {
                        categories.UnionWith(definition.AugmentationPool);
                    }
                }
            }

            if (AttunementTrees.Find(item.AttunementTree, settings) is { } tree)
            {
                categories.UnionWith(tree.Nodes.SelectMany(node => node.AugmentationCategories));
            }
        }

        var text = Text(settings);

        return categoriesBySlot.Values
            .SelectMany(categories => categories)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                category => category,
                IReadOnlyList<ItemTypeText> (category) =>
                [
                    .. text.ItemTypes.Where(type =>
                        categoriesBySlot.TryGetValue(type.Slot, out var carried) && carried.Contains(category)),
                ],
                StringComparer.OrdinalIgnoreCase);
    }

    public static AugmentationCategoryText Category(SettingsFile settings, AugmentationDocument augmentation) =>
        Text(settings).Categories.FirstOrDefault(category =>
            string.Equals(category.CategoryTag, augmentation.CategoryTag, StringComparison.OrdinalIgnoreCase))
        ?? Text(settings).Categories.FirstOrDefault(category => category.CategoryTag is null)
        ?? throw new InvalidOperationException(
            "The settings state no augmentation card text for the category " + augmentation.CategoryTag + " and no default.");

    public static string? HeroRead(SettingsFile settings, AugmentationDocument augmentation, string? hero) =>
        augmentation.DescriptionsByHero is { Count: > 0 } byHero
            ? hero is not null && byHero.ContainsKey(hero)
                ? hero
                : settings.Heroes.Select(entry => entry.Name).FirstOrDefault(byHero.ContainsKey)
            : null;

    public static string? Description(AugmentationDocument augmentation, int rank, string? hero = null)
    {
        if ((hero is not null && augmentation.DescriptionsByHero?.GetValueOrDefault(hero) is { } read
                ? read
                : augmentation.Description) is not { } description)
        {
            return null;
        }

        foreach (var run in augmentation.RankRuns ?? [])
        {
            if (run.Printed.Count == 0) continue;

            description = description.Replace(
                run.Literal, run.Printed[Math.Clamp(rank, 1, run.Printed.Count) - 1], StringComparison.Ordinal);
        }

        return description;
    }

    public static IEnumerable<(string Stat, decimal Value)> Granted(
        EnchantDocument enchant, SettingsFile settings, int itemLevel)
    {
        var power = Power(settings, itemLevel);

        foreach (var stat in enchant.Stats ?? [])
        {
            if (stat.Coefficient == 0) continue;

            var scaled = stat.Coefficient * power;
            var value = stat.Lookup is { } lookup ? CurveReading.At(lookup, scaled) : scaled;

            yield return (StatOf(stat.Attribute, settings), (decimal)value);
        }
    }

    private static string StatOf(string attribute, SettingsFile settings) =>
        settings.StatCalculation.Attributes
            .FirstOrDefault(pair => string.Equals(pair.Value.Attribute, attribute, StringComparison.Ordinal))
            .Key
        ?? throw new InvalidOperationException(
            $"No stat in the settings reads the attribute {attribute}, so an enchant granting it has no line.");
}

public static class RankSources
{
    public static IReadOnlyList<string> Parse(string? value) =>
        value is null
            ? []
            : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public static string? Write(IEnumerable<string> sources) =>
        string.Join(',', sources) is { Length: > 0 } written ? written : null;
}
