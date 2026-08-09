using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

public interface ITooltipService
{
    Task<IResult> Get(EntityType type, int id, TooltipQuery query);
}

public sealed class TooltipService(IEntityStoreService store, IFragmentService fragments) : ITooltipService
{
    public async Task<IResult> Get(EntityType type, int id, TooltipQuery query)
    {
        if (query.Hero is { } named && store.Hero(named) is null)
        {
            return Lookups.NoSuchHero(named, store.Settings.Heroes);
        }

        if (query.Rarity is { } rung && store.Rarity(rung, gem: false) is null && store.Rarity(rung, gem: true) is null)
        {
            return Results.Problem(
                title: "No such rarity.",
                detail: $"'{rung}' is not a rarity. The rungs are " +
                    string.Join(", ", store.Settings.Rarities.Select(r => $"{r.DisplayName} ({r.Tier})")) +
                    ", and a gem's are " +
                    string.Join(", ", store.Settings.Rarities.Select(r => r.GemDisplayName)) + ".",
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

        if (outcome != Lookup.Found)
        {
            return Lookups.Problem(outcome, type, id, hero, store.All(type, id));
        }

        var fragment = await fragments.Render<Components.Tooltip.Tooltip>(new()
        {
            ["Entity"] = document!,
            ["Query"] = query,
            ["Hero"] = hero,
        });

        return Results.Content(fragment, "text/html; charset=utf-8");
    }
}
