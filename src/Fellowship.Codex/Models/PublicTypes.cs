using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public sealed record Category(string Slug, string Label);

public sealed record PublicType(
    EntityType Type,
    string Slug,
    string Label,
    string Noun,
    Category Category,
    bool Listed,
    int Position);

public static class Categories
{
    public static Category Spells { get; } = new("spells", "Spells");

    public static Category Items { get; } = new("items", "Items");

    public static Category Dungeons { get; } = new("dungeons", "Dungeons");

    public static Category Npcs { get; } = new("npcs", "NPCs");

    public static IReadOnlyList<Category> All { get; } = [Spells, Items, Dungeons, Npcs];

    public static Category? Find(string? slug) =>
        All.FirstOrDefault(category => string.Equals(category.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static string NoSuchCategory(string? asked) =>
        $"'{asked}' is not a category. The categories are {string.Join(", ", All.Select(one => one.Slug))}.";
}

public static class PublicTypes
{
    private static readonly (EntityType Type, string Label, string Noun, Category Category)[] Table =
    [
        (EntityType.Ability, "Abilities", "ability", Categories.Spells),
        (EntityType.Effect, "Effects", "effect", Categories.Spells),
        (EntityType.Talent, "Talents", "talent", Categories.Spells),
        (EntityType.Trait, "Traits", "trait", Categories.Spells),
        (EntityType.GemTrait, "Gem Traits", "gem trait", Categories.Spells),
        (EntityType.SetBonus, "Set Bonuses", "set bonus", Categories.Spells),
        (EntityType.SetBonusPower, "Set Bonus Powers", "set bonus power", Categories.Spells),
        (EntityType.TraitPower, "Trait Powers", "trait power", Categories.Spells),
        (EntityType.WeaponPower, "Weapon Powers", "weapon power", Categories.Spells),
        (EntityType.RelicPower, "Relic Powers", "relic power", Categories.Spells),
        (EntityType.LegendaryPower, "Legendary Powers", "legendary power", Categories.Spells),
        (EntityType.TalentSlot, "Talent Slots", "talent slot", Categories.Spells),
        (EntityType.MountPower, "Mount Powers", "mount power", Categories.Spells),
        (EntityType.Hero, "Heroes", "hero", Categories.Spells),

        (EntityType.Item, "Items", "item", Categories.Items),
        (EntityType.Blessing, "Blessings", "blessing", Categories.Items),
        (EntityType.Gem, "Gems", "gem", Categories.Items),
        (EntityType.Currency, "Currencies", "currency", Categories.Items),
        (EntityType.Mount, "Mounts", "mount", Categories.Items),
        (EntityType.CompanionPet, "Companion Pets", "companion pet", Categories.Items),
        (EntityType.LegendaryStat, "Legendary Stats", "legendary stat", Categories.Items),
        (EntityType.GemType, "Gem Types", "gem type", Categories.Items),

        (EntityType.Dungeon, "Dungeons", "dungeon", Categories.Dungeons),
        (EntityType.Modifier, "Modifiers", "modifier", Categories.Dungeons),

        (EntityType.Creature, "Creatures", "creature", Categories.Npcs),
    ];

    private static readonly EntityType[] Unlisted =
    [
        EntityType.TalentSlot,
        EntityType.MountPower,
        EntityType.Hero,
        EntityType.LegendaryStat,
        EntityType.GemType,
        EntityType.Modifier,
    ];

    public static IReadOnlyList<PublicType> All { get; } = Verified(
    [
        .. Table.Select((row, position) => new PublicType(
            row.Type,
            EntityTypes.Slug(row.Type),
            row.Label,
            row.Noun,
            row.Category,
            !Unlisted.Contains(row.Type),
            position)),
    ]);

    private static IReadOnlyList<PublicType> Verified(IReadOnlyList<PublicType> table)
    {
        var missing = Enum.GetValues<EntityType>().Except(table.Select(type => type.Type)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}, so the codex states neither a category nor a label for " +
                (missing.Length == 1 ? "it." : "them."));
        }

        var twice = table
            .GroupBy(type => type.Type)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (twice.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", twice)} {(twice.Length == 1 ? "has" : "have")} more than one row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}, which states each type once.");
        }

        var unnamed = Unlisted.Except(table.Select(type => type.Type)).ToArray();

        if (unnamed.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", unnamed)} {(unnamed.Length == 1 ? "is" : "are")} named in " +
                $"{nameof(PublicTypes)}.{nameof(Unlisted)} and {(unnamed.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(Table)}.");
        }

        return table;
    }

    private static readonly IReadOnlyDictionary<EntityType, PublicType> ByType =
        All.ToDictionary(type => type.Type);

    public static IReadOnlyList<PublicType> Listed { get; } =
        [.. All.Where(type => type.Listed).OrderBy(type => type.Position)];

    public static IReadOnlyList<PublicType> In(Category category) =>
        [.. Listed.Where(type => type.Category == category)];

    public static bool IsListed(EntityType type) => Find(type).Listed;

    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    public static string NotInCategory(string? asked, Category category) =>
        $"'{asked}' is not a {category.Label} type. The {category.Label} types are " +
        $"{string.Join(", ", In(category).Select(type => type.Slug))}.";

    private static readonly string Accepted =
        string.Join(", ", Listed.Select(type => type.Slug));

    public static string Label(EntityType type) => Find(type).Label;

    public static string Noun(EntityType type) => Find(type).Noun;

    public static Category Category(EntityType type) => Find(type).Category;

    public static string Where(EntityType type) =>
        $"/{Category(type).Slug}?type={Uri.EscapeDataString(Find(type).Slug)}";

    public static string? DisplayNameOf(EntityDocument document) => document.Name;

    public static string NameOf(EntityDocument document)
    {
        if (DisplayNameOf(document) is { } name) return name;

        var type = EntityTypes.TypeOf(document);
        return $"{Titled(Find(type).Noun)} {document.Id}";
    }

    private static PublicType Find(EntityType type) => ByType[type];

    private static string Titled(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];
}
