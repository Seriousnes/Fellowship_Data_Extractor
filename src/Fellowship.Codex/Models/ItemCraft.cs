using System.Globalization;

using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class CraftSlots
{
    public const string GemSocket = "GemSocket";
    public const string TraitTree = "TraitTree";
    public const string GrantedAbility = "GrantedAbility";
    public const string RandomRolls = "RandomRolls";

    public const string GemPower = "GemPower";
    public const string ItemTrait = "ItemTrait";
    public const string AbilityRank = "AbilityRank";
}

public readonly record struct KnobTurn(string Key, string? Value);

public sealed record CraftSlot(string SlotName, int Ordinal, IReadOnlyList<string> Accepts)
{
    public int Capacity { get; init; } = 1;

    public string Key => KeyOf(SlotName, Ordinal);

    public static string KeyOf(string slotName, int ordinal) =>
        $"{slotName}:{ordinal.ToString(CultureInfo.InvariantCulture)}";
}

public static class ItemCraft
{
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
                CraftSlots.TraitTree => new CraftSlot(type, ordinal, [name])
                {
                    Capacity = TraitTrees.Capacity(item.TraitTree, settings) ?? 0,
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

    public static ItemSlotDefinition Definition(string name, SettingsFile settings) =>
        settings.StatCalculation.SlotDefinitions.TryGetValue(name, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"The settings define no item slot {name}, so the editor knows no slot type for it. The slots " +
                $"defined are {string.Join(", ", settings.StatCalculation.SlotDefinitions.Keys)}.");

    public static string? Defining(ModifierKind kind, EntityDocument? row, SettingsFile settings) =>
        settings.StatCalculation.SlotDefinitions.TryGetValue(kind.ToString(), out var definition) &&
        (definition.SlotType != CraftSlots.ItemTrait ||
         row is ItemTraitDocument { CategoryTag: { } tag } && definition.ItemTraitPool.Contains(tag, StringComparer.Ordinal))
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

    public static ModifierKind? KindOfTrait(EntityDocument? row, SettingsFile settings) =>
        row is ItemTraitDocument { CategoryTag: { } tag }
            ? settings.StatCalculation.SlotDefinitions
                .Where(pair => pair.Value.SlotType == CraftSlots.ItemTrait &&
                               pair.Value.ItemTraitPool.Contains(tag, StringComparer.Ordinal))
                .Select(pair => KindOf(pair.Key, settings))
                .FirstOrDefault(kind => kind is not null)
            : null;

    public const int SameIdentity = 2;

    public static int MaxRank(CraftCandidate candidate, IEntityStoreService store) =>
        Definition(candidate.Accepts, store.Settings).SlotType switch
        {
            CraftSlots.AbilityRank => Find<BlessingDocument>(store, EntityType.Blessing, candidate.Id)?.MaxRank ?? 1,
            CraftSlots.ItemTrait or CraftSlots.TraitTree =>
                TooltipView.ItemTrait(store, candidate.Id, null)?.MaxRank ?? 1,
            _ => int.MaxValue,
        };

    public static string? Forbids(
        ItemDocument item,
        TooltipQuery query,
        CraftSlot slot,
        CraftCandidate candidate,
        IEntityStoreService store)
    {
        var floor = Definition(candidate.Accepts, store.Settings).MinimumItemLevel;

        if ((query.ItemLevel ?? store.DropItemLevel(item)) < floor) return CraftReasons.BelowItemLevel(floor);

        if (slot.SlotName == CraftSlots.TraitTree)
        {
            var tree = TooltipView.TreeName(store.Settings);

            if (query.ImbuedTraits.Any(trait => trait.Id == candidate.Id)) return CraftReasons.AlreadyAttuned(tree);
            if (query.ImbuedTraits.Count >= slot.Capacity) return CraftReasons.NoRoom(tree);

            var ceiling = MaxRank(candidate, store);
            return candidate.Amount < 1 || candidate.Amount > ceiling ? CraftReasons.PastRank(ceiling) : null;
        }

        if (KindOf(candidate.Accepts, store.Settings) is not { } kind) return null;

        if (TooltipView.TypeOf(kind, store.Settings) == EntityType.Blessing)
        {
            if (Find<BlessingDocument>(store, EntityType.Blessing, candidate.Id) is { } blessing &&
                item.Slot is { } worn &&
                !(blessing.Slots ?? []).Contains(worn, StringComparer.OrdinalIgnoreCase))
            {
                return CraftReasons.NotInPool(worn);
            }
        }

        var type = TooltipView.Type(kind, store.Settings);

        if (type.MaximumLimit is { } cap && query.Modifiers.Count(modifier => modifier.Kind == kind) >= cap)
        {
            return type.MaximumLimitWarning
                ?? throw new InvalidOperationException($"The {kind} modifier type states a cap and no warning.");
        }

        if (query.Modifiers.Count(
            modifier => modifier.Kind == kind && modifier.Id == candidate.Id) >= SameIdentity)
        {
            return CraftReasons.TwoOfAKind;
        }

        var max = MaxRank(candidate, store);

        return candidate.Amount < 1 || candidate.Amount > max ? CraftReasons.PastRank(max) : null;
    }

    public static TooltipQuery Without(TooltipQuery query, CraftSlot slot) => slot.SlotName switch
    {
        CraftSlots.GemSocket => query with { Gem = null },

        CraftSlots.TraitTree => query,

        _ => query with
        {
            Modifiers = slot.Ordinal < query.Modifiers.Count
                ? [.. query.Modifiers.Take(slot.Ordinal), .. query.Modifiers.Skip(slot.Ordinal + 1)]
                : query.Modifiers,
        },
    };

    public static TooltipQuery With(
        TooltipQuery query, CraftSlot slot, CraftCandidate candidate, SettingsFile settings) => slot.SlotName switch
    {
        CraftSlots.TraitTree => query with
        {
            ImbuedTraits = [.. query.ImbuedTraits, new ImbuedTrait(candidate.Id, candidate.Amount)],
        },

        CraftSlots.GemSocket => query with { Gem = candidate.Id },

        _ => query with
        {
            Modifiers = Set(
                [.. query.Modifiers],
                slot.Ordinal,
                new ItemModifier(KindOf(candidate.Accepts, settings)!.Value, candidate.Id, candidate.Amount)),
        },
    };

    public static List<TValue> Set<TValue>(List<TValue> values, int at, TValue value)
    {
        if (at >= values.Count) values.Add(value); else values[at] = value;

        return values;
    }

    private static TDocument? Find<TDocument>(IEntityStoreService store, EntityType type, int id)
        where TDocument : EntityDocument =>
        store.All(type, id).OfType<TDocument>().FirstOrDefault();
}

public sealed record CraftCandidate(string Accepts, int Id, string Name, int Amount);

public static class CraftReasons
{
    public const string TwoOfAKind = "Two of one is the most an item rolls.";

    public static string AlreadyAttuned(string tree) => $"Already in the {tree}.";

    public static string NoRoom(string tree) => $"Every row of the {tree} is filled.";

    public static string NotInPool(string slot) => $"Does not roll on a {slot}.";

    public static string BelowItemLevel(int floor) =>
        $"Rolls from item level {floor.ToString(CultureInfo.InvariantCulture)}.";

    public static string PastRank(int max) => $"Ranks 1 to {max} only.";
}
