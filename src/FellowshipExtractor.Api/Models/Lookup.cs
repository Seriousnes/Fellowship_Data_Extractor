using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public enum Lookup
{
    Found,
    NoSuchId,

    NeedsHero,

    NotForThatHero,
}

public static class EntityStore
{
    public static bool Reads(EntityDocument document, HeroEntry hero) =>
        document switch
        {
            ItemDocument item => item.Heroes is { } heroes
                ? heroes.Contains(ParseEnum<Hero>(hero.Name))
                : (item.ArmorType is null || item.ArmorType == ParseEnumOrNull<ArmorType>(hero.ArmorType)) &&
                  (item.PrimaryStat is null || item.PrimaryStat == ParseEnum<PrimaryStat>(hero.PrimaryStat)),
            TalentSlotDocument slot => slot.Hero == ParseEnum<Hero>(hero.Name),
            _ => true,
        };

    private static T ParseEnum<T>(string name) where T : struct, Enum =>
        Enum.TryParse<T>(name, out var value) ? value : throw new FormatException($"'{name}' names no {typeof(T).Name}.");

    private static T? ParseEnumOrNull<T>(string? name) where T : struct, Enum =>
        name is null ? null : ParseEnum<T>(name);
}

public static class Lookups
{
    public static IResult Problem(
        Lookup outcome, EntityType type, int id, HeroEntry? hero, IReadOnlyList<EntityDocument> readings)
    {
        var noun = PublicTypes.Noun(type);

        return outcome switch
        {
            Lookup.NoSuchId => Results.Problem(
                title: "No such id.",
                detail: $"The build has no {noun} {id}.",
                statusCode: StatusCodes.Status404NotFound),

            Lookup.NeedsHero => Results.Problem(
                title: "This id reads differently per hero.",
                detail: $"The build has {readings.Count} readings of {noun} {id} and no hero was given. " +
                    "Add hero=<name> to pick one. The readings are " +
                    string.Join("; ", readings.Select(Describe)) + ".",
                statusCode: StatusCodes.Status400BadRequest),

            Lookup.NotForThatHero => Results.Problem(
                title: "Not for that hero.",
                detail: $"The build has no reading of {noun} {id} for {hero!.Name}.",
                statusCode: StatusCodes.Status404NotFound),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    public static IResult NoSuchHero(string named, IReadOnlyList<HeroEntry> roster) => Results.Problem(
        title: "No such hero.",
        detail: $"'{named}' is not a hero. The roster is " +
            string.Join(", ", roster.Select(hero => hero.Name).Order()) + ".",
        statusCode: StatusCodes.Status400BadRequest);

    private static string Describe(EntityDocument document) => document switch
    {
        ItemDocument item => $"{PublicTypes.NameOf(item)} " +
            $"[{item.ArmorType?.ToString() ?? "any"}/{item.PrimaryStat?.ToString() ?? "any"}]",
        TalentSlotDocument slot => $"{PublicTypes.NameOf(slot)} [{slot.Hero}]",
        _ => PublicTypes.NameOf(document),
    };
}
