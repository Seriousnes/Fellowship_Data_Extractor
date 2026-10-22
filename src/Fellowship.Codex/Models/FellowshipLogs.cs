using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class FellowshipLogs
{
    public const int GemPowerBase = 1_000_000;
    public const int TalentBase = 2_000_000;
    public const int TraitBase = 3_000_000;
    public const int BlessingBase = 4_000_000;

    private const string GemPowerField = "power";
    private const string TraitRankField = "rank";
    private const string BlessingLevelField = "level";
    private const string ImbuedPointsField = "points";
    private const string AttributeValueField = "value";

    private const string ArmorStat = "Armor";
    private const string WeaponSlot = "Weapon";
    private const string NeckSlot = "Neck";
    private const string WeaponTree = "weaponTraits";
    private const string NeckTree = "neckTraits";

    public static readonly IReadOnlyDictionary<int, string> Attributes =
        new Dictionary<int, string>
        {
            [1] = "Stamina",
            [2] = "Strength",
            [3] = "Intellect",
            [4] = "Agility",
            [10] = "CritChance",
            [14] = "Haste",
            [15] = "Expertise",
            [23] = "Spirit",
            [26] = ArmorStat,
        };

    public static IReadOnlyList<JsonElement> Gear(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var worn = new List<JsonElement>();

            foreach (var element in payload.EnumerateArray()) worn.AddRange(Gear(element));

            return worn;
        }

        return payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("gear", out var gear) &&
            gear.ValueKind == JsonValueKind.Array
                ? [.. gear.EnumerateArray()]
                : [payload];
    }

    public static IReadOnlyList<JsonElement> Combatants(JsonElement payload) =>
        payload.ValueKind == JsonValueKind.Array
            ? [.. payload.EnumerateArray().SelectMany(element => Combatants(element))]
            : payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("gear", out _)
                ? [payload]
                : [];

    public static TooltipQuery Read(
        JsonElement gear,
        IEntityStoreService store,
        out int itemId,
        out IReadOnlyList<string> unresolved,
        JsonElement owner = default)
    {
        var missing = new List<string>();

        itemId = Int(gear, "id") ?? 0;

        var stats = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var modifiers = new List<ItemModifier>();
        var imbued = new List<ImbuedTrait>();
        decimal? armor = null;

        foreach (var attribute in Array(gear, "attributes"))
        {
            if (Int(attribute, "id") is not { } id || !Attributes.TryGetValue(id, out var stat))
            {
                missing.Add($"attribute id {Int(attribute, "id")} names no stat.");
                continue;
            }

            var value = Decimal(attribute, AttributeValueField) ?? 0m;

            if (stat.Equals(ArmorStat, StringComparison.OrdinalIgnoreCase)) { armor = value; continue; }

            stats[stat] = stats.TryGetValue(stat, out var existing) ? [.. existing, value] : [value];
        }

        foreach (var power in Array(gear, "gemPowers"))
        {
            Modifier(ModifierKind.Essence, power, GemPowerBase, GemPowerField, EntityType.GemType);
        }

        foreach (var trait in Array(gear, "traits"))
        {
            Modifier(ModifierKind.Trait, trait, TraitBase, TraitRankField, TooltipView.TraitType);
        }

        foreach (var blessing in Array(gear, "blessings"))
        {
            if (Int(blessing, "id") is not { } logged || Blessing(logged, store) is not { } resolved)
            {
                missing.Add($"blessing id {Int(blessing, "id")} resolves to no ability rank row.");
                continue;
            }

            modifiers.Add(new ItemModifier(
                ModifierKind.Blessing, resolved, Int(blessing, BlessingLevelField) ?? 1));
        }

        foreach (var trait in Tree(gear, owner, Worn(store, itemId)))
        {
            if (Int(trait, ImbuedPointsField) is not { } points || points <= 0) continue;
            if (Int(trait, "id") is not { } logged || logged < TraitBase) continue;

            var id = logged - TraitBase;

            if (store.All(TooltipView.TraitType, id).Count == 0)
            {
                missing.Add($"imbued trait id {logged} names no trait.");
                continue;
            }

            imbued.Add(new ImbuedTrait(id, points));
        }

        void Modifier(ModifierKind kind, JsonElement entry, int under, string amountField, EntityType type)
        {
            if (Int(entry, "id") is not { } logged) return;

            var id = logged - under;

            if (store.All(type, id).Count == 0)
            {
                missing.Add($"{kind} id {logged} names nothing in the export.");
                return;
            }

            modifiers.Add(new ItemModifier(kind, id, Int(entry, amountField) ?? 1));
        }

        unresolved = missing;

        return new TooltipQuery
        {
            Rarity = Int(gear, "quality")?.ToString(CultureInfo.InvariantCulture),
            ItemLevel = Int(gear, "itemLevel"),
            Upgrades = Int(gear, "upgrades"),
            MaxUpgrades = Int(gear, "maxUpgrades"),
            NoSocket = Bool(gear, "hasGemSocket") == false,
            Gem = gear.ValueKind == JsonValueKind.Object && gear.TryGetProperty("gem", out var gem)
                ? Int(gem, "id")
                : null,
            PotentialPower = Decimal(gear, "potentialPower"),
            Armor = armor,
            Stats = stats,
            Modifiers = modifiers,
            ImbuedTraits = imbued,
            AllocatedTraits = imbued.Count > 0 ? imbued.Count : null,
        };
    }

    public static int? Blessing(int loggedId, IEntityStoreService store)
    {
        var rank = loggedId - BlessingBase;

        foreach (var blessing in store.Entities.OfType<BlessingDocument>())
        {
            if (blessing.AbilityRank == rank) return blessing.Id;
        }

        return null;
    }

    public static JsonNode Write(ItemDocument item, TooltipQuery query, IEntityStoreService store)
    {
        var entry = new JsonObject
        {
            ["id"] = item.Id,
            ["name"] = PublicTypes.NameOf(item),
            ["icon"] = item.Icon,
            ["itemLevel"] = query.ItemLevel,
            ["upgrades"] = query.Upgrades,
            ["maxUpgrades"] = query.MaxUpgrades,
            ["hasGemSocket"] = !query.NoSocket,
            ["potentialPower"] = query.PotentialPower,
        };

        if (Tier(query.Rarity, store) is { } quality) entry["quality"] = quality;

        if (query.Gem is { } gem)
        {
            var named = store.All(EntityType.Gem, gem).OfType<GemDocument>().FirstOrDefault();

            entry["gem"] = new JsonObject
            {
                ["id"] = gem,
                ["icon"] = named?.Icon,
                ["name"] = named is null ? null : PublicTypes.NameOf(named),
            };
        }

        var attributes = new JsonArray();

        foreach (var (stat, values) in query.Stats)
        {
            if (AttributeId(stat) is not { } id) continue;

            foreach (var value in values)
            {
                attributes.Add(new JsonObject { ["id"] = id, ["name"] = stat, [AttributeValueField] = value });
            }
        }

        if (query.Armor is { } armor)
        {
            attributes.Add(new JsonObject
            {
                ["id"] = AttributeId(ArmorStat),
                ["name"] = ArmorStat,
                [AttributeValueField] = armor,
            });
        }

        if (attributes.Count > 0) entry["attributes"] = attributes;

        Kind(ModifierKind.Essence, "gemPowers", GemPowerBase, GemPowerField);
        Kind(ModifierKind.Trait, "traits", TraitBase, TraitRankField);

        var blessings = new JsonArray();

        foreach (var modifier in query.Modifiers.Where(row => row.Kind == ModifierKind.Blessing))
        {
            if (RankOf(modifier.Id, store) is not { } logged) continue;

            blessings.Add(new JsonObject
            {
                ["id"] = logged,
                [BlessingLevelField] = modifier.Amount,
                ["name"] = Name(EntityType.Blessing, modifier.Id, store),
            });
        }

        if (blessings.Count > 0) entry["blessings"] = blessings;

        if (query.ImbuedTraits.Count > 0)
        {
            var tree = new JsonArray();

            foreach (var trait in query.ImbuedTraits)
            {
                tree.Add(new JsonObject
                {
                    ["id"] = trait.Id + TraitBase,
                    [BlessingLevelField] = 1,
                    [ImbuedPointsField] = trait.Rank,
                    ["name"] = Name(TooltipView.TraitType, trait.Id, store),
                });
            }

            entry[NeckSlot.Equals(item.Slot, StringComparison.OrdinalIgnoreCase) ? NeckTree : WeaponTree] = tree;
        }

        void Kind(ModifierKind kind, string field, int under, string amountField)
        {
            var entries = new JsonArray();

            foreach (var modifier in query.Modifiers.Where(row => row.Kind == kind))
            {
                entries.Add(new JsonObject
                {
                    ["id"] = modifier.Id + under,
                    [amountField] = modifier.Amount,
                    ["name"] = Name(TooltipView.TypeOf(kind), modifier.Id, store),
                });
            }

            if (entries.Count > 0) entry[field] = entries;
        }

        return entry;
    }

    private static string? Worn(IEntityStoreService store, int itemId) =>
        store.All(EntityType.Item, itemId).OfType<ItemDocument>().FirstOrDefault()?.Slot;

    private static IEnumerable<JsonElement> Tree(JsonElement gear, JsonElement owner, string? slot)
    {
        var field = NeckSlot.Equals(slot, StringComparison.OrdinalIgnoreCase) ? NeckTree : WeaponTree;

        if (slot is null ||
            (!WeaponSlot.Equals(slot, StringComparison.OrdinalIgnoreCase) &&
                !NeckSlot.Equals(slot, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        var values = Array(gear, field).ToArray();

        return values.Length > 0 ? values : Array(owner, field);
    }

    private static int? AttributeId(string stat)
    {
        foreach (var (id, name) in Attributes)
        {
            if (name.Equals(stat, StringComparison.OrdinalIgnoreCase)) return id;
        }

        return null;
    }

    private static int? Tier(string? rarity, IEntityStoreService store)
    {
        if (rarity is not { Length: > 0 }) return null;
        if (int.TryParse(rarity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier)) return tier;

        return store.Rarity(rarity, gem: false)?.Tier;
    }

    private static int? RankOf(int blessingId, IEntityStoreService store) =>
        store.All(EntityType.Blessing, blessingId).OfType<BlessingDocument>().FirstOrDefault()
            is { AbilityRank: { } rank }
            ? rank + BlessingBase
            : null;

    private static string? Name(EntityType type, int id, IEntityStoreService store) =>
        store.All(type, id).FirstOrDefault() is { } document ? PublicTypes.NameOf(document) : null;

    private static IEnumerable<JsonElement> Array(JsonElement owner, string field) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static int? Int(JsonElement owner, string field) =>
        Number(owner, field) is { } number && number.TryGetInt32(out var value) ? value : null;

    private static decimal? Decimal(JsonElement owner, string field) =>
        Number(owner, field) is { } number && number.TryGetDecimal(out var value) ? value : null;

    private static JsonElement? Number(JsonElement owner, string field) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number
            ? value
            : null;

    private static bool? Bool(JsonElement owner, string field) =>
        owner.ValueKind == JsonValueKind.Object &&
        owner.TryGetProperty(field, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
