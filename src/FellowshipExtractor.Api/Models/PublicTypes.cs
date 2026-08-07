using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record PublicType(EntityType Type, string Slug, string Label, string Noun, string Group);

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
        (EntityType.LegendaryStat, "Legendary Stats", "legendary stat", "Items"),
        (EntityType.Currency, "Currencies", "currency", "Items"),
        (EntityType.GemType, "Gem Types", "gem type", "Gems"),
        (EntityType.GemTier, "Gem Tiers", "gem tier", "Gems"),
        (EntityType.GemTrait, "Gem Traits", "gem trait", "Gems"),
        (EntityType.Ability, "Abilities", "ability", "Abilities"),
        (EntityType.Blessing, "Blessings", "blessing", "Abilities"),
        (EntityType.Effect, "Effects", "effect", "Abilities"),
        (EntityType.Talent, "Talents", "talent", "Characters"),
        (EntityType.TalentSlot, "Talent Slots", "talent slot", "Characters"),
        (EntityType.Creature, "Creatures", "creature", "Characters"),
        (EntityType.CompanionPet, "Companion Pets", "companion pet", "Characters"),
        (EntityType.Mount, "Mounts", "mount", "Characters"),
        (EntityType.MountPower, "Mount Powers", "mount power", "Characters"),
    ];

    public static IReadOnlyList<PublicType> All { get; } = Verified(
        [.. Table.Select(row => new PublicType(row.Type, EntityTypes.Slug(row.Type), row.Label, row.Noun, row.Group))]);

    private static IReadOnlyList<PublicType> Verified(IReadOnlyList<PublicType> table)
    {
        var missing = Enum.GetValues<EntityType>().Except(table.Select(type => type.Type)).ToArray();

        return missing.Length == 0
            ? table
            : throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}.");
    }

    private static readonly IReadOnlyDictionary<EntityType, PublicType> ByType =
        All.ToDictionary(type => type.Type);

    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted = string.Join(", ", All.Select(type => type.Slug));

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
