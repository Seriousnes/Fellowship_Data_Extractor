using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fellowship.SDK.Documents;

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

    /// <summary>
    /// The route back from a slug to the member it names. The one map either direction reads, so what
    /// <see cref="Slug"/> writes always reads back and there is no second table of names to drift from
    /// the first.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, EntityType> BySlug =
        Enum.GetValues<EntityType>().ToDictionary(Slug, type => type, StringComparer.Ordinal);

    /// <summary>The member a slug names, or the failure stating which slug named none.</summary>
    public static EntityType Parse(string slug) =>
        TryParse(slug, out var type) ? type : throw new FormatException($"'{slug}' names no {nameof(EntityType)}.");

    public static bool TryParse(string slug, out EntityType type) => BySlug.TryGetValue(slug, out type);

    /// <summary>
    /// Resolved and cached the first time a member is asked for, not all twenty-one at once, so a
    /// member added without its class only fails the read of that member and leaves the other twenty
    /// working.
    /// </summary>
    private static readonly ConcurrentDictionary<EntityType, Type> ByMember = new();

    /// <summary>
    /// The document class for a member, found by name in the assembly holding
    /// <see cref="EntityDocument"/> rather than listed here, so a member added without its class fails
    /// the first read that names it rather than deserialising to nothing.
    /// </summary>
    public static Type Document(EntityType type) => ByMember.GetOrAdd(type, Resolve);

    private static Type Resolve(EntityType type) =>
        typeof(EntityDocument).Assembly.GetType($"{typeof(EntityDocument).Namespace}.{type}")
        ?? throw new InvalidOperationException(
            $"{type} names no document class in {typeof(EntityDocument).Assembly.GetName().Name}.");
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
    public override EntityType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } slug && EntityTypes.TryParse(slug, out var type)
            ? type
            : throw new JsonException($"'{reader.GetString()}' names no {nameof(EntityType)}.");

    public override void Write(Utf8JsonWriter writer, EntityType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(EntityTypes.Slug(value));
}
