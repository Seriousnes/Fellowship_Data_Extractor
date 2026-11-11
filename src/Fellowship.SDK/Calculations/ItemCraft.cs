using System.Globalization;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.SDK.Calculations;

public static class CraftSlots
{
    public const string GemSocket = "GemSocket";
    public const string AttunementTree = "TraitTree";
    public const string GrantedAbility = "GrantedAbility";
    public const string RandomRolls = "RandomRolls";

    public const string GemPower = "GemPower";
    public const string Augmentation = "ItemTrait";
    public const string AbilityRank = "AbilityRank";
}

public sealed record CraftSlot(string SlotName, int Ordinal, IReadOnlyList<string> Accepts)
{
    public int Capacity { get; init; } = 1;

    public string Key => KeyOf(SlotName, Ordinal);

    public static string KeyOf(string slotName, int ordinal) =>
        $"{slotName}:{ordinal.ToString(CultureInfo.InvariantCulture)}";
}

public static class ItemCraft
{
    public const int SameIdentity = 2;

    public static IReadOnlyList<CraftSlot> Slots(
        ItemDocument item, SettingsFile settings, RarityEntry? rarity)
    {
        if (rarity is null || item.RuleSet is not { } ruleSet) return [];
        if (!settings.StatCalculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return [];
        if (!byTier.TryGetValue(rarity.Tier, out var added)) return [];

        var slots = new List<CraftSlot>();
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int Next(string name)
        {
            var at = ordinals.GetValueOrDefault(name);
            ordinals[name] = at + 1;
            return at;
        }

        void Add(string name)
        {
            var type = Definition(name, settings).SlotType;
            var ordinal = Next(type);

            slots.Add(type switch
            {
                CraftSlots.AttunementTree => new CraftSlot(type, ordinal, [name])
                {
                    Capacity = AttunementTrees.Capacity(item.AttunementTree, settings) ?? 0,
                },
                CraftSlots.GemSocket => new CraftSlot(type, ordinal, [name]),
                _ => new CraftSlot(type, ordinal, []),
            });
        }

        foreach (var name in added.GuaranteedSlots) Add(name);
        foreach (var special in added.SpecialSlots) Add(special.SlotName);

        IReadOnlyList<string> accepts =
            [.. added.RandomizedSlots.Distinct(StringComparer.OrdinalIgnoreCase)];

        for (var roll = 0; roll < added.RandomRolls; roll++)
        {
            slots.Add(new CraftSlot(CraftSlots.RandomRolls, Next(CraftSlots.RandomRolls), accepts));
        }

        return slots;
    }

    public static int RandomRolls(ItemDocument item, SettingsFile settings, RarityEntry? rarity)
    {
        if (rarity is null || item.RuleSet is not { } ruleSet) return 0;
        if (!settings.StatCalculation.RarityAdditions.TryGetValue(ruleSet, out var byTier)) return 0;

        return byTier.TryGetValue(rarity.Tier, out var added) ? added.RandomRolls : 0;
    }

    public static ItemSlotDefinition Definition(string name, SettingsFile settings) =>
        settings.StatCalculation.SlotDefinitions.TryGetValue(name, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"The settings define no item slot {name}, so it has no slot type. The slots " +
                $"defined are {string.Join(", ", settings.StatCalculation.SlotDefinitions.Keys)}.");

    public static string? Defining(ModifierKind kind, EntityDocument? row, SettingsFile settings) =>
        settings.StatCalculation.SlotDefinitions.TryGetValue(kind.ToString(), out var definition) &&
        (definition.SlotType != CraftSlots.Augmentation ||
         row is AugmentationDocument { CategoryTag: { } tag } && definition.AugmentationPool.Contains(tag, StringComparer.Ordinal))
            ? kind.ToString()
            : null;

    public static ModifierKind? KindOf(string accepted, SettingsFile settings) =>
        Enum.TryParse<ModifierKind>(accepted, out var kind) && settings.StatCalculation.SlotDefinitions.ContainsKey(accepted)
            ? kind
            : null;

    public static ModifierKind? KindOfSlotType(string slotType, SettingsFile settings) =>
        settings.StatCalculation.SlotDefinitions
            .Where(pair => pair.Value.SlotType == slotType)
            .Select(pair => KindOf(pair.Key, settings))
            .FirstOrDefault(kind => kind is not null);

    public static ModifierKind? KindOfAugmentation(EntityDocument? row, SettingsFile settings) =>
        row is AugmentationDocument { CategoryTag: { } tag }
            ? settings.StatCalculation.SlotDefinitions
                .Where(pair => pair.Value.SlotType == CraftSlots.Augmentation &&
                               pair.Value.AugmentationPool.Contains(tag, StringComparer.Ordinal))
                .Select(pair => KindOf(pair.Key, settings))
                .FirstOrDefault(kind => kind is not null)
            : null;

    /// <summary>Entity type a modifier of the kind names.</summary>
    public static EntityType TypeOf(ModifierKind kind, SettingsFile settings) =>
        Definition(kind.ToString(), settings) switch
        {
            { SlotType: CraftSlots.Augmentation, AugmentationPool: [var category] }
                when AugmentationTypes.ByCategory.TryGetValue(category, out var type) => type,
            { SlotType: CraftSlots.AbilityRank } => EntityType.Glyph,
            { SlotType: CraftSlots.GemPower } => EntityType.Radiance,
            var definition => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, $"{kind} is a {definition.SlotType} slot, which names no entity type."),
        };

    public static List<TValue> Set<TValue>(List<TValue> values, int at, TValue value)
    {
        if (at >= values.Count) values.Add(value); else values[at] = value;

        return values;
    }
}
