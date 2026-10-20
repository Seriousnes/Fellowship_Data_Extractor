using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class CatalogEndpoint
{
    public static RouteHandlerBuilder MapCatalog(this IEndpointRouteBuilder group) =>
        group.MapGet("/catalog", Handle)
            .WithName("GetCatalog")
            .WithSummary("The menus, entity types and filter facets the browser reads.")
            .Produces<CatalogResponse>();

    private static IResult Handle(ICodexService codex, IMenuService menus, IEntityStoreService store)
    {
        var rows = store.Entities
            .Where(document => PublicTypes.IsListed(document))
            .ToArray();

        return Results.Ok(new CatalogResponse(
            codex.Count,
            Categories.All,
            [
                .. codex.Types
                    .OrderBy(type => type.Position)
                    .Select(type => new CatalogType(
                        type.Slug, type.Label, type.Category.Slug, type.Count, type.Position)),
            ],
            menus.Menu,
            [.. codex.Facets.Select(facet => codex.View(facet, rows))],
            [.. codex.Flags.Select(flag => new CatalogFlag(flag.Key, flag.Label))],
            [.. codex.Measures.Select(measure => new CatalogMeasure(measure.Key, measure.Label, measure.Unit))],
            [.. codex.Sortings.Select(sorting => new CatalogSorting(sorting.Key, sorting.Label))],
            [.. store.Settings.Heroes.Select(hero => new CatalogHero(hero.Name, hero.Color))],
            [
                .. store.Settings.Rarities.Select(rarity => new CatalogRarity(
                    rarity.Tier, rarity.DisplayName, rarity.GemDisplayName)),
            ]));
    }
}
