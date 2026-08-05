using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fellowship.SDK;

/// <summary>
/// What a row is. The shipped tag table is not this: ItemID holds items, mounts, currencies, gem
/// types, gem tiers and set bonuses, and CRCharacterAugmentationAsset holds nine things of which a
/// hero's talent is one.
/// </summary>
public enum EntityType
{
    Ability,
    CompanionPet,
    Creature,
    Currency,
    Effect,
    Finesse,
    GemTier,
    GemTrait,
    GemType,
    Item,
    LegendaryStat,
    Mount,
    MountPower,
    RelicPower,
    SetBonus,
    SetBonusPower,
    Talent,
    TalentSlot,
    Trait,
    TraitPower,
    WeaponPower,
}

public static class EntityTypes
{
    /// <summary>
    /// The route segment a type is addressed by: its name, lower cased, with a hyphen where the name
    /// changes word. Derived rather than listed, so a type added to the enum is addressable without
    /// a second edit.
    /// </summary>
    public static string Slug(EntityType type)
    {
        var name = type.ToString();
        var slug = new StringBuilder();

        for (var at = 0; at < name.Length; at++)
        {
            if (at > 0 && char.IsUpper(name[at])) slug.Append('-');
            slug.Append(char.ToLowerInvariant(name[at]));
        }

        return slug.ToString();
    }
}

/// <summary>
/// A type written as its <see cref="EntityTypes.Slug"/> rather than as the enum member. The slug is
/// the addressable form, so a reference nested in a line spells its type the way the line carrying
/// that type spells it and a reader can join the two on it. Four of the twenty-one spellings differ
/// from their slug by more than case, and a reference carrying one of those joins to nothing.
///
/// Both directions read <see cref="EntityTypes.Slug"/>, so what is written always reads back and
/// there is no second table of names to drift from the first.
/// </summary>
public sealed class EntityTypeSlugConverter : JsonConverter<EntityType>
{
    private static readonly Dictionary<string, EntityType> BySlug =
        Enum.GetValues<EntityType>().ToDictionary(EntityTypes.Slug, type => type, StringComparer.Ordinal);

    public override EntityType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } slug && BySlug.TryGetValue(slug, out var type)
            ? type
            : throw new JsonException($"'{reader.GetString()}' names no {nameof(EntityType)}.");

    public override void Write(Utf8JsonWriter writer, EntityType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(EntityTypes.Slug(value));
}
