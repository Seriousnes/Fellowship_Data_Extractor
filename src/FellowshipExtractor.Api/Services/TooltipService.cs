using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface ITooltipService
{
    Task<IResult> Get(EntityType type, int id, IQueryCollection request);
}

public sealed class TooltipService(IEntityStoreService store, IFragmentService fragments) : ITooltipService
{
    public async Task<IResult> Get(EntityType type, int id, IQueryCollection request)
    {
        var query = TooltipQuery.Parse(request, store.Stats, out var error);

        if (error is not null)
        {
            return Results.Problem(
                title: "The request parameters could not be read.",
                detail: $"{error} Stat parameters are {string.Join(", ", store.Stats.Names)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (query.Hero is { } named && store.Hero(named) is null)
        {
            return Results.Problem(
                title: "No such hero.",
                detail: $"'{named}' is not a hero. The roster is " +
                    string.Join(", ", store.Vocabulary.Heroes.Select(h => h.Name).Order()) + ".",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (query.Rarity is { } rung && store.Rarity(rung, gem: false) is null && store.Rarity(rung, gem: true) is null)
        {
            return Results.Problem(
                title: "No such rarity.",
                detail: $"'{rung}' is not a rarity. The rungs are " +
                    string.Join(", ", store.Vocabulary.Rarities.Select(r => $"{r.DisplayName} ({r.Tier})")) +
                    ", and a gem's are " +
                    string.Join(", ", store.Vocabulary.Rarities.Select(r => r.GemDisplayName)) + ".",
                statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var imbued in query.Traits)
        {
            if (store.All(TooltipView.TraitType, imbued.Id).Count > 0) continue;

            return Results.Problem(
                title: "No such item trait.",
                detail: $"The build has no item trait {imbued.Id}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var outcome = store.Resolve(type, id, query.Hero, out var document, out var hero);

        var noun = PublicTypes.Noun(type);
        var readings = store.All(type, id);

        if (outcome == Lookup.Found)
        {
            var fragment = await fragments.Render<Components.Tooltip.Tooltip>(new()
            {
                ["Entity"] = document!,
                ["Query"] = query,
                ["Hero"] = hero,
            });

            return Results.Content(fragment, "text/html; charset=utf-8");
        }

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

    private static string Describe(EntityDocument document) => document switch
    {
        ItemDocument item => $"{PublicTypes.NameOf(item)} " +
            $"[{item.ArmorType?.ToString() ?? "any"}/{item.PrimaryStat?.ToString() ?? "any"}]",
        TalentSlotDocument slot => $"{PublicTypes.NameOf(slot)} [{slot.Hero}]",
        _ => PublicTypes.NameOf(document),
    };
}
