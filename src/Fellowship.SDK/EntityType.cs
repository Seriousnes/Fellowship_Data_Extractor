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
    ///
    /// Case insensitive, because a slug arrives typed by hand as often as it arrives clicked, and a
    /// route or a query value spelled with the wrong case should resolve rather than answer 404. The
    /// comparer only forgives case, not punctuation: a single-word member's own name is its slug bar
    /// case, so <c>Talent</c> resolves the same as <c>talent</c>, but a multi-word member's name
    /// carries no hyphen and still resolves to nothing, so <c>SetBonusPower</c> does not stand in for
    /// <c>set-bonus-power</c>. No two slugs differ only by case, so nothing here becomes ambiguous by
    /// forgiving it.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, EntityType> BySlug =
        Enum.GetValues<EntityType>().ToDictionary(Slug, type => type, StringComparer.OrdinalIgnoreCase);

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

    /// <summary>
    /// The type a document is, read the same way <see cref="Document"/> reads it in reverse: every
    /// document class is named exactly its <see cref="EntityType"/> member, so the type is read
    /// straight off the runtime type rather than through a second table that could disagree with it.
    /// The one answer, so a line the SDK writes, a line it reads back, and the name a document falls
    /// back to when it prints none of its own all agree on what a document is.
    /// </summary>
    public static EntityType TypeOf(EntityDocument document) =>
        Enum.TryParse<EntityType>(document.GetType().Name, out var type)
            ? type
            : throw new InvalidOperationException($"{document.GetType().Name} names no {nameof(EntityType)}.");
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
