using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class CatalogEndpoint
{
    public static RouteHandlerBuilder MapCatalog(this IEndpointRouteBuilder group) =>
        group.MapGet("/catalog", Handle)
            .WithName("GetCatalog")
            .WithSummary("The menus, entity types and filter facets the browser is driven by.");

    private static IResult Handle(ICodexService codex, IMenuService menus, IEntityStoreService store)
    {
        var rows = store.Entities
            .Where(document => PublicTypes.IsListed(EntityTypes.TypeOf(document)))
            .ToArray();

        return Results.Ok(new
        {
            entities = codex.Count,

            categories = Categories.All.Select(category => new { category.Slug, category.Label }),

            types = codex.Types
                .OrderBy(type => type.Position)
                .Select(type => new
                {
                    type.Slug, type.Label, Category = type.Category.Slug, type.Count, type.Position,
                }),

            menu = menus.Menu,
            facets = codex.Facets.Select(facet => codex.View(facet, rows)),
            flags = codex.Flags.Select(flag => new { flag.Key, flag.Label }),
            measures = codex.Measures.Select(measure => new { measure.Key, measure.Label, measure.Unit }),
            sortings = codex.Sortings.Select(sorting => new { sorting.Key, sorting.Label }),

            heroes = store.Settings.Heroes.Select(hero => new
            {
                hero.Name,
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
