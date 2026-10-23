using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface ITooltipService
{
    Task<IResult> Get(EntityType type, int id, TooltipQuery query, string origin);

    Task<IResult> Get(EntityType type, int id, string build, TooltipQuery query, string origin);
}

public sealed class TooltipService(
    IEntityStoreService store, IFragmentService fragments, IExportHistoryService history) : ITooltipService
{
    public async Task<IResult> Get(EntityType type, int id, string build, TooltipQuery query, string origin)
    {
        var outcome = store.Resolve(type, id, query.Hero, out var document, out var hero);

        if (outcome != Lookup.Found)
        {
            return Lookups.Problem(outcome, type, id, hero, store.All(type, id));
        }

        if (history.At(build, document!) is not { } then)
        {
            return Results.Problem(
                title: "No such version.",
                detail: $"Build {build} has no {PublicTypes.Noun(type)} {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var fragment = await fragments.Render<Components.Tooltip.Tooltip>(new()
        {
            ["Entity"] = then,
            ["Query"] = query,
            ["Hero"] = hero,
            ["Origin"] = origin,
        });

        return Results.Content(fragment, "text/html; charset=utf-8");
    }

    public async Task<IResult> Get(EntityType type, int id, TooltipQuery query, string origin)
    {
        if (query.Hero is { } named && store.Hero(named) is null)
        {
            return Lookups.NoSuchHero(named, store.Settings.Heroes);
        }

        if (query.Rarity is { } rarity && store.Rarity(rarity, gem: false) is null &&
            store.Rarity(rarity, gem: true) is null)
        {
            return Results.Problem(
                title: "No such rarity.",
                detail: $"'{rarity}' is not a rarity. The rarities are " +
                    string.Join(", ", store.Settings.Rarities.Select(r => $"{r.DisplayName} ({r.Tier})")) +
                    ", and a gem's rarities are " +
                    string.Join(", ", store.Settings.Rarities.Select(r => r.GemDisplayName)) + ".",
                statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var attuned in query.Attunements)
        {
            if (TooltipView.Augmentation(store, attuned.Id, null) is not null) continue;

            return Results.Problem(
                title: "No such item augmentation.",
                detail: $"The build has no item augmentation {attuned.Id}.",
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
            ["Origin"] = origin,
        });

        return Results.Content(fragment, "text/html; charset=utf-8");
    }
}
