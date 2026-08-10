using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public static class CatalogEndpoint
{
    public static RouteHandlerBuilder MapCatalog(this IEndpointRouteBuilder group) =>
        group.MapGet("/catalog", Handle)
            .WithName("GetCatalog")
            .WithSummary("The menus, entity types and filter facets the browser is driven by.");

    private static IResult Handle(ICodexService codex, IEntityStoreService store)
    {
        var rows = store.Entities;

        return Results.Ok(new
        {
            entities = codex.Count,

            types = codex.Types.Select(type => new { type.Slug, type.Label, type.Group, type.Count }),
            menu = codex.Menu,
            facets = codex.Facets.Select(facet => codex.View(facet, rows)),
            flags = codex.Flags.Select(flag => new { flag.Key, flag.Label }),
            measures = codex.Measures.Select(measure => new { measure.Key, measure.Label, measure.Unit }),

            heroes = store.Settings.Heroes.Select(hero => new
            {
                hero.Name,
                hero.ArmorType,
                hero.PrimaryStat,
                hero.Color,
            }),
            rarities = store.Settings.Rarities.Select(rarity => new
            {
                rarity.Tier,
                rarity.DisplayName,
                rarity.GemDisplayName,
            }),
        });
    }
}
