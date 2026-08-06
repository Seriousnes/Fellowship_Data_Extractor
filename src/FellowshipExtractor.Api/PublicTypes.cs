using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed record PublicType(string Table, string Slug, string Label, string Noun, string Group);

public static class PublicTypes
{
    public static IReadOnlyList<PublicType> All { get; } =
    [
        new PublicType("ItemID", "items", "Items", "item", "Items"),
        new PublicType("ItemTrait.ID", "traits", "Traits", "trait", "Items"),
        new PublicType("GameplayEffect", "effect", "Effect", "effect", "Abilities"),
        new PublicType("GameplayAbility", "ability", "Ability", "ability", "Abilities"),
        new PublicType("CRCharacterAugmentationAsset", "talent", "Talent", "talent", "Characters"),
        new PublicType("CRAICharacter", "creatures", "Creatures", "creature", "Characters"),
    ];

    private static readonly IReadOnlyDictionary<string, PublicType> ByTable =
        All.ToDictionary(type => type.Table, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, PublicType> BySlug =
        All.ToDictionary(type => type.Slug, StringComparer.OrdinalIgnoreCase);

    public static bool IsPublic(string? table) => table is not null && ByTable.ContainsKey(table);

    public static string NoSuchType(string? asked) =>
        $"'{asked}' is not a type. The types are {Accepted}.";

    private static readonly string Accepted = string.Join(", ", All.Select(type => type.Slug));

    public static string? SlugOf(string? table) => Find(table)?.Slug;

    public static string? TableOf(string? slug) =>
        slug is not null && BySlug.TryGetValue(slug, out var type) ? type.Table : null;

    public static string Label(string? table) => Find(table)?.Label ?? "Entries";

    public static string Noun(string? table) => Find(table)?.Noun ?? "entry";

    public static string Group(string? table) => Find(table)?.Group ?? string.Empty;

    public static string? DisplayNameOf(Entity entity) =>
        entity.DisplayName is { } name && WrittenAsWords(name) ? name : null;

    private static bool WrittenAsWords(string name)
    {
        for (var at = 0; at < name.Length; at++)
        {
            if (name[at] is '.' or '_' or '[' or ']') return false;

            if (at > 0 && char.IsUpper(name[at]) &&
                (char.IsLower(name[at - 1]) || char.IsDigit(name[at - 1])))
            {
                return false;
            }
        }

        return true;
    }

    public static string NameOf(Entity entity)
    {
        if (DisplayNameOf(entity) is { } name) return name;

        var noun = Titled(Noun(entity.Table));
        return entity.NetId is { } netId ? $"{noun} {netId}" : noun;
    }

    private static PublicType? Find(string? table) =>
        table is not null && ByTable.TryGetValue(table, out var type) ? type : null;

    private static string Titled(string noun) => char.ToUpperInvariant(noun[0]) + noun[1..];
}
