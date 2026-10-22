using System.Globalization;

using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class CraftSlots
{
    public const string GemSocket = "GemSocket";
    public const string TraitTree = "TraitTree";
    public const string SetBonus = "SetBonus";
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

    public static ModifierKind? KindOf(string accepted, SettingsFile settings) =>
        Definition(accepted, settings).SlotType switch
        {
            CraftSlots.ItemTrait or CraftSlots.TraitTree => ModifierKind.Trait,
            CraftSlots.AbilityRank => ModifierKind.Blessing,
            CraftSlots.GemPower => ModifierKind.Essence,
            _ => null,
        };

    public const int SameIdentity = 2;

    public const int Blessings = 2;

    public static int MaxRank(CraftCandidate candidate, IEntityStoreService store) =>
        KindOf(candidate.Accepts, store.Settings) switch
        {
            ModifierKind.Blessing => Find<BlessingDocument>(store, EntityType.Blessing, candidate.Id)?.MaxRank ?? 1,
            ModifierKind.Trait => Find<TraitDocument>(store, TooltipView.TraitType, candidate.Id)?.MaxRank ?? 1,
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
            if (query.ImbuedTraits.Any(trait => trait.Id == candidate.Id)) return CraftReasons.AlreadyImbued;
            if (query.ImbuedTraits.Count >= slot.Capacity) return CraftReasons.NoRoom;

            var ceiling = MaxRank(candidate, store);
            return candidate.Amount < 1 || candidate.Amount > ceiling ? CraftReasons.PastRank(ceiling) : null;
        }

        if (KindOf(candidate.Accepts, store.Settings) is not { } kind) return null;

        if (kind == ModifierKind.Blessing)
        {
            if (Find<BlessingDocument>(store, EntityType.Blessing, candidate.Id) is { } blessing &&
                item.Slot is { } worn &&
                !(blessing.Slots ?? []).Contains(worn, StringComparer.OrdinalIgnoreCase))
            {
                return CraftReasons.NotInPool(worn);
            }

            if (query.Modifiers.Count(modifier => modifier.Kind == ModifierKind.Blessing) >= Blessings)
            {
                return CraftReasons.TwoBlessings;
            }
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

    public const string TwoBlessings = "Two blessings is the most an item has.";

    public const string AlreadyImbued = "Already imbued.";

    public const string NoRoom = "Every imbued trait slot is filled.";

    public static string NotInPool(string slot) => $"Does not roll on a {slot}.";

    public static string BelowItemLevel(int floor) =>
        $"Rolls from item level {floor.ToString(CultureInfo.InvariantCulture)}.";

    public static string PastRank(int max) => $"Ranks 1 to {max} only.";
}
