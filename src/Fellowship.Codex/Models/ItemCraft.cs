using System.Globalization;

using Fellowship.Codex.Services;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public readonly record struct KnobTurn(ReadingParameter Key, string? Value);

public static class ItemCraftEditing
{
    extension(ItemCraft)
    {
        public static int MaxRank(CraftCandidate candidate, IEntityStoreService store) =>
            ItemCraft.Definition(candidate.Accepts, store.Settings).SlotType switch
            {
                CraftSlots.AbilityRank => Find<GlyphDocument>(store, EntityType.Glyph, candidate.Id)?.MaxRank ?? 1,
                CraftSlots.Augmentation or CraftSlots.AttunementTree =>
                    store.Export.Augmentation(candidate.Id, null)?.MaxRank ?? 1,
                _ => int.MaxValue,
            };

        public static string? Forbids(
            ItemDocument item,
            TooltipQuery query,
            CraftSlot slot,
            CraftCandidate candidate,
            IEntityStoreService store)
        {
            var floor = ItemCraft.Definition(candidate.Accepts, store.Settings).MinimumItemLevel;

            if ((query.ItemLevel ?? store.DropItemLevel(item)) < floor) return CraftReasons.BelowItemLevel(floor);

            if (slot.SlotName == CraftSlots.AttunementTree)
            {
                var tree = TooltipView.TreeName(store.Settings);

                if (query.Attunements.Any(augmentation => augmentation.Id == candidate.Id)) return CraftReasons.AlreadyAttuned(tree);
                if (query.Attunements.Count >= slot.Capacity) return CraftReasons.NoRoom(tree);

                var ceiling = ItemCraft.MaxRank(candidate, store);
                return candidate.Amount < 1 || candidate.Amount > ceiling ? CraftReasons.PastRank(ceiling) : null;
            }

            if (ItemCraft.KindOf(candidate.Accepts, store.Settings) is not { } kind) return null;

            if (ItemCraft.TypeOf(kind, store.Settings) == EntityType.Glyph)
            {
                if (Find<GlyphDocument>(store, EntityType.Glyph, candidate.Id) is { } glyph &&
                    item.Slot is { } worn &&
                    !(glyph.Slots ?? []).Contains(worn, StringComparer.OrdinalIgnoreCase))
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
                modifier => modifier.Kind == kind && modifier.Id == candidate.Id) >= ItemCraft.SameIdentity)
            {
                return CraftReasons.TwoOfAKind;
            }

            var max = ItemCraft.MaxRank(candidate, store);

            return candidate.Amount < 1 || candidate.Amount > max ? CraftReasons.PastRank(max) : null;
        }

        public static TooltipQuery Without(TooltipQuery query, CraftSlot slot) => slot.SlotName switch
        {
            CraftSlots.GemSocket => query with { Gem = null },

            CraftSlots.AttunementTree => query,

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
            CraftSlots.AttunementTree => query with
            {
                Attunements = [.. query.Attunements, new Attunement(candidate.Id, candidate.Amount)],
            },

            CraftSlots.GemSocket => query with { Gem = candidate.Id },

            _ => query with
            {
                Modifiers = ItemCraft.Set(
                    [.. query.Modifiers],
                    slot.Ordinal,
                    new ItemModifier(ItemCraft.KindOf(candidate.Accepts, settings)!.Value, candidate.Id, candidate.Amount)),
            },
        };
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
