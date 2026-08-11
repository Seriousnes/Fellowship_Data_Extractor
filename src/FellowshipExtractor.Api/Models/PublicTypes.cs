using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record PublicType(
    EntityType Type, string Slug, string Label, string Noun, string Group, bool Listed, bool Tabbed, int Position);

public static class PublicTypes
{
    private static readonly (EntityType Type, string Label, string Noun, string Group)[] Table =
    [
        (EntityType.Item, "Items", "item", "Items"),
        (EntityType.Trait, "Traits", "trait", "Items"),
        (EntityType.SetBonus, "Set Bonuses", "set bonus", "Items"),
        (EntityType.SetBonusPower, "Set Bonus Powers", "set bonus power", "Items"),
        (EntityType.WeaponPower, "Weapon Powers", "weapon power", "Items"),
        (EntityType.RelicPower, "Relic Powers", "relic power", "Items"),
        (EntityType.TraitPower, "Trait Powers", "trait power", "Items"),
        (EntityType.LegendaryPower, "Legendary Powers", "legendary power", "Items"),
        (EntityType.LegendaryStat, "Legendary Stats", "legendary stat", "Items"),
        (EntityType.Currency, "Currencies", "currency", "Items"),
        (EntityType.GemType, "Gem Types", "gem type", "Gems"),
        (EntityType.Gem, "Gems", "gem", "Gems"),
        (EntityType.GemTrait, "Gem Traits", "gem trait", "Gems"),
        (EntityType.Ability, "Abilities", "ability", "Abilities"),
        (EntityType.Blessing, "Blessings", "blessing", "Abilities"),
        (EntityType.Effect, "Effects", "effect", "Abilities"),
        (EntityType.Talent, "Talents", "talent", "Characters"),
        (EntityType.TalentSlot, "Talent Slots", "talent slot", "Characters"),
        (EntityType.Creature, "Creatures", "creature", "Characters"),
        (EntityType.Dungeon, "Dungeons", "dungeon", "Characters"),
        (EntityType.CompanionPet, "Companion Pets", "companion pet", "Characters"),
        (EntityType.Mount, "Mounts", "mount", "Characters"),
        (EntityType.MountPower, "Mount Powers", "mount power", "Characters"),
    ];

    private static readonly EntityType[] Tabbed =
    [
        EntityType.Item, EntityType.Ability, EntityType.Effect, EntityType.Talent,
        EntityType.Blessing, EntityType.GemTrait, EntityType.Trait, EntityType.SetBonus,
        EntityType.GemType, EntityType.Creature, EntityType.Dungeon, EntityType.Mount, EntityType.CompanionPet,
        EntityType.Currency, EntityType.LegendaryPower, EntityType.WeaponPower,
        EntityType.RelicPower, EntityType.TraitPower, EntityType.SetBonusPower,
    ];

    private static readonly EntityType[] Untabbed = [EntityType.Gem, EntityType.LegendaryStat];

    private static readonly EntityType[] Unlisted = [EntityType.TalentSlot, EntityType.MountPower];

    private static readonly EntityType[] Ordered =
    [
        .. Tabbed,
        .. Table.Select(row => row.Type).Where(Untabbed.Contains),
        .. Table.Select(row => row.Type).Where(Unlisted.Contains),
    ];

    public static IReadOnlyList<PublicType> All { get; } = Verified(
    [
        .. Table.Select(row => new PublicType(
            row.Type,
            EntityTypes.Slug(row.Type),
            row.Label,
            row.Noun,
            row.Group,
            !Unlisted.Contains(row.Type),
            Tabbed.Contains(row.Type),
            Array.IndexOf(Ordered, row.Type))),
    ]);

    private static IReadOnlyList<PublicType> Verified(IReadOnlyList<PublicType> table)
    {
        var missing = Enum.GetValues<EntityType>().Except(table.Select(type => type.Type)).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}.");
        }

        var placed = Tabbed.Concat(Untabbed).Concat(Unlisted).ToArray();
        var unplaced = Enum.GetValues<EntityType>().Except(placed).ToArray();
        var twice = placed.GroupBy(type => type).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();

        if (unplaced.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", unplaced)} {(unplaced.Length == 1 ? "is" : "are")} in none of " +
                $"{nameof(Tabbed)}, {nameof(Untabbed)} and {nameof(Unlisted)}, so the codex states neither a " +
                "tab for it nor a reason it holds none.");
        }

        if (twice.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", twice)} {(twice.Length == 1 ? "is" : "are")} in more than one of " +
                $"{nameof(Tabbed)}, {nameof(Untabbed)} and {nameof(Unlisted)}, which together state each type once.");
        }

        return table;
    }

    private static readonly IReadOnlyDictionary<EntityType, PublicType> ByType =
        All.ToDictionary(type => type.Type);

    public static IReadOnlyList<PublicType> Tabs { get; } =
        [.. All.Where(type => type.Tabbed).OrderBy(type => type.Position)];

    public static bool Listed(EntityType type) => Find(type).Listed;

    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted =
        string.Join(", ", All.Where(type => type.Listed).OrderBy(type => type.Position).Select(type => type.Slug));

    public static string Label(EntityType type) => Find(type).Label;

    public static string Noun(EntityType type) => Find(type).Noun;

    public static string Group(EntityType type) => Find(type).Group;

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
