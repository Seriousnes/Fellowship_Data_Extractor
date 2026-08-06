using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fellowship.SDK.Documents;

namespace Fellowship.SDK;

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

    private static readonly IReadOnlyDictionary<string, EntityType> BySlug =
        Enum.GetValues<EntityType>().ToDictionary(Slug, type => type, StringComparer.OrdinalIgnoreCase);

    public static EntityType Parse(string slug) =>
        TryParse(slug, out var type) ? type : throw new FormatException($"'{slug}' names no {nameof(EntityType)}.");

    public static bool TryParse(string slug, out EntityType type) => BySlug.TryGetValue(slug, out type);

    private const string Suffix = "Document";

    private static readonly ConcurrentDictionary<EntityType, Type> ByMember = new();

    public static Type Document(EntityType type) => ByMember.GetOrAdd(type, Resolve);

    private static Type Resolve(EntityType type) =>
        typeof(EntityDocument).Assembly.GetType($"{typeof(EntityDocument).Namespace}.{type}{Suffix}")
        ?? throw new InvalidOperationException(
            $"{type} names no document class in {typeof(EntityDocument).Assembly.GetName().Name}.");

    public static EntityType TypeOf(EntityDocument document)
    {
        var name = document.GetType().Name;
        var member = name.EndsWith(Suffix, StringComparison.Ordinal) ? name[..^Suffix.Length] : name;

        return Enum.TryParse<EntityType>(member, out var type)
            ? type
            : throw new InvalidOperationException($"{name} names no {nameof(EntityType)}.");
    }
}

public sealed class EntityTypeSlugConverter : JsonConverter<EntityType>
{
    public override EntityType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() is { } slug && EntityTypes.TryParse(slug, out var type)
            ? type
            : throw new JsonException($"'{reader.GetString()}' names no {nameof(EntityType)}.");

    public override void Write(Utf8JsonWriter writer, EntityType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(EntityTypes.Slug(value));
}
