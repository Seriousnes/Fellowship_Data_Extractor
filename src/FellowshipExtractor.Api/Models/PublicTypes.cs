using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record PublicType(Fellowship.SDK.EntityType Type, string Slug, string Label, string Noun, string Group);

public static class PublicTypes
{
    private static readonly (Fellowship.SDK.EntityType Type, string Label, string Noun, string Group)[] Table =
    [
        (Fellowship.SDK.EntityType.Item, "Items", "item", "Items"),
        (Fellowship.SDK.EntityType.Trait, "Traits", "trait", "Items"),
        (Fellowship.SDK.EntityType.SetBonus, "Set Bonuses", "set bonus", "Items"),
        (Fellowship.SDK.EntityType.SetBonusPower, "Set Bonus Powers", "set bonus power", "Items"),
        (Fellowship.SDK.EntityType.WeaponPower, "Weapon Powers", "weapon power", "Items"),
        (Fellowship.SDK.EntityType.RelicPower, "Relic Powers", "relic power", "Items"),
        (Fellowship.SDK.EntityType.TraitPower, "Trait Powers", "trait power", "Items"),
        (Fellowship.SDK.EntityType.LegendaryStat, "Legendary Stats", "legendary stat", "Items"),
        (Fellowship.SDK.EntityType.Currency, "Currencies", "currency", "Items"),
        (Fellowship.SDK.EntityType.GemType, "Gem Types", "gem type", "Gems"),
        (Fellowship.SDK.EntityType.GemTier, "Gem Tiers", "gem tier", "Gems"),
        (Fellowship.SDK.EntityType.GemTrait, "Gem Traits", "gem trait", "Gems"),
        (Fellowship.SDK.EntityType.Ability, "Abilities", "ability", "Abilities"),
        (Fellowship.SDK.EntityType.Effect, "Effects", "effect", "Abilities"),
        (Fellowship.SDK.EntityType.Finesse, "Finesses", "finesse", "Abilities"),
        (Fellowship.SDK.EntityType.Talent, "Talents", "talent", "Characters"),
        (Fellowship.SDK.EntityType.TalentSlot, "Talent Slots", "talent slot", "Characters"),
        (Fellowship.SDK.EntityType.Creature, "Creatures", "creature", "Characters"),
        (Fellowship.SDK.EntityType.CompanionPet, "Companion Pets", "companion pet", "Characters"),
        (Fellowship.SDK.EntityType.Mount, "Mounts", "mount", "Characters"),
        (Fellowship.SDK.EntityType.MountPower, "Mount Powers", "mount power", "Characters"),
    ];

    public static IReadOnlyList<PublicType> All { get; } = Verified(
        [.. Table.Select(row => new PublicType(row.Type, EntityTypes.Slug(row.Type), row.Label, row.Noun, row.Group))]);

    private static IReadOnlyList<PublicType> Verified(IReadOnlyList<PublicType> table)
    {
        var missing = Enum.GetValues<Fellowship.SDK.EntityType>().Except(table.Select(type => type.Type)).ToArray();

        return missing.Length == 0
            ? table
            : throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(PublicTypes)}.{nameof(Table)}.");
    }

    private static readonly IReadOnlyDictionary<Fellowship.SDK.EntityType, PublicType> ByType =
        All.ToDictionary(type => type.Type);

    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted = string.Join(", ", All.Select(type => type.Slug));

    public static string Label(Fellowship.SDK.EntityType type) => Find(type).Label;

    public static string Noun(Fellowship.SDK.EntityType type) => Find(type).Noun;

    public static string Group(Fellowship.SDK.EntityType type) => Find(type).Group;

    public static string? DisplayNameOf(EntityDocument document) => document.Name;

    public static string NameOf(EntityDocument document)
    {
        if (DisplayNameOf(document) is { } name) return name;

        var type = EntityTypes.TypeOf(document);
        return $"{Titled(Find(type).Noun)} {document.Id}";
    }

    private static PublicType Find(Fellowship.SDK.EntityType type) => ByType[type];

    private static string Titled(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];
}
