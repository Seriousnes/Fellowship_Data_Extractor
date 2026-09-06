using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

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

public sealed record Fault(string Title, string Detail, int Status);

public static class Lookups
{
    public static Fault Trouble(
        Lookup outcome, EntityType type, int id, HeroEntry? hero, IReadOnlyList<EntityDocument> readings)
    {
        var noun = PublicTypes.Noun(type);

        return outcome switch
        {
            Lookup.NoSuchId => new Fault(
                "No such id.",
                $"The build has no {noun} {id}.",
                StatusCodes.Status404NotFound),

            Lookup.NeedsHero => new Fault(
                "This id reads differently per hero.",
                $"The build has {readings.Count} readings of {noun} {id} and no hero was given. " +
                    "Add hero=<name> to pick one. The readings are " +
                    string.Join("; ", readings.Select(Describe)) + ".",
                StatusCodes.Status400BadRequest),

            Lookup.NotForThatHero => new Fault(
                "Not for that hero.",
                $"The build has no reading of {noun} {id} for {hero!.Name}.",
                StatusCodes.Status404NotFound),

            _ => new Fault(
                "Unreadable.",
                $"The build could not read {noun} {id}.",
                StatusCodes.Status500InternalServerError),
        };
    }

    public static Fault NoHero(string named, IReadOnlyList<HeroEntry> roster) => new(
        "No such hero.",
        $"'{named}' is not a hero. The roster is " +
            string.Join(", ", roster.Select(hero => hero.Name).Order()) + ".",
        StatusCodes.Status400BadRequest);

    public static IResult Problem(
        Lookup outcome, EntityType type, int id, HeroEntry? hero, IReadOnlyList<EntityDocument> readings) =>
        Respond(Trouble(outcome, type, id, hero, readings));

    public static IResult NoSuchHero(string named, IReadOnlyList<HeroEntry> roster) =>
        Respond(NoHero(named, roster));

    private static IResult Respond(Fault fault) =>
        Results.Problem(title: fault.Title, detail: fault.Detail, statusCode: fault.Status);

    private static string Describe(EntityDocument document) => document switch
    {
        ItemDocument item => $"{PublicTypes.NameOf(item)} " +
            $"[{item.ArmorType?.ToString() ?? "any"}/{item.PrimaryStat?.ToString() ?? "any"}]",
        TalentSlotDocument slot => $"{PublicTypes.NameOf(slot)} [{slot.Hero}]",
        _ => PublicTypes.NameOf(document),
    };
}
