using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

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
                $"The build has {readings.Count} readings of {noun} {id} and no hero was named. " +
                    "Name a hero to pick one. The readings are " +
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
