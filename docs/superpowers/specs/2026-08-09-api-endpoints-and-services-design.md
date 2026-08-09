# API endpoints and services design

**Goal:** Every route the API answers lives in a file of its own under `Endpoints/`, addressed by a
literal path that names one entity type. Every type registered in the container lives under
`Services/` behind an interface. `Program.cs` composes the application and registers nothing else.

## The route surface

Each of the 22 `EntityType` members declares two routes:

```
/api/item/{id}                  /api/item/{id}/tooltip
/api/ability/{id}               /api/ability/{id}/tooltip
/api/effect/{id}                /api/effect/{id}/tooltip
/api/gem/{id}                   /api/gem/{id}/tooltip
```

and so on for `blessing`, `companion-pet`, `creature`, `currency`, `gem-trait`, `gem-type`,
`legendary-power`, `legendary-stat`, `mount`, `mount-power`, `relic-power`, `set-bonus`,
`set-bonus-power`, `talent`, `talent-slot`, `trait`, `trait-power` and `weapon-power`. The path
segment is `EntityTypes.Slug(type)`.

Three routes answer across types and keep their paths: `/api/search`, `/api/entities` and
`/api/catalog`. `/api/search` and `/api/entities` bind request models of their own;
`/api/catalog` takes no parameters.

Every template is literal, so no slug is parsed at runtime and an address naming no type is a
routing 404 before any handler runs.

`CodexViewService.Tip` builds `/api/{slug}/{id}/tooltip`, which is the one place the entity page
takes its refetch address from.

## The record

`/api/{slug}/{id}` answers one `PublicEntity`.

An item id and a talent slot id can read differently per hero, so those two request models carry
`Hero`. Given a hero, the endpoint answers that reading. Given none where the build holds more than
one, it answers 400 naming the readings, which is the response `/api/{slug}/{id}/tooltip` already
gives through `Lookup.NeedsHero`. The other 20 types resolve one reading per id and carry no `Hero`.

An id the build does not hold answers 404 naming the noun and the id.

## The endpoint files

```
Endpoints/
  ApiEndpoints.cs
  Item/
    ItemGroup.cs
    ItemRecordEndpoint.cs
    ItemTooltipEndpoint.cs
  Ability/
    AbilityGroup.cs
    AbilityRecordEndpoint.cs
    AbilityTooltipEndpoint.cs
  Browsing/
    SearchEndpoint.cs
    EntitiesEndpoint.cs
    CatalogEndpoint.cs
```

with a folder for each of the 22 types.

A group owns one type's prefix and the metadata both its routes share:

```csharp
public static class ItemGroup
{
    public static RouteGroupBuilder MapItem(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/item").WithTags(PublicTypes.Label(EntityType.Item));

        group.MapItemRecord();
        group.MapItemTooltip();

        return group;
    }
}
```

An endpoint file holds one route, the request model it binds and the handler it calls:

```csharp
public static class ItemTooltipEndpoint
{
    public static RouteHandlerBuilder MapItemTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetItemTooltip")
            .WithSummary("One item's tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] ItemTooltipRequest request, HttpContext http, ITooltipService tooltips) =>
        tooltips.Get(EntityType.Item, request.Id, request.ToQuery(http.Request.Query));
}
```

`ApiEndpoints.MapApiEndpoints` calls the 22 group methods and the three browsing endpoints, and is
the one call `Program.cs` makes.

## The request models

A request model is a record whose properties are the parameters that endpoint accepts. The binding
source is inferred: a property named for a route token binds from the route, every other property
binds from the query. Nullability states whether a parameter is optional, so an optional parameter
is a nullable property and carries nothing else.

```csharp
public sealed record ItemTooltipRequest
{
    public int Id { get; init; }

    public string? Hero { get; init; }

    public string? Rarity { get; init; }

    [Range(0, 10_000)]
    public int? ItemLevel { get; init; }

    public int? Upgrades { get; init; }

    public int? MaxUpgrades { get; init; }

    public bool? NoSocket { get; init; }

    public decimal? PotentialPower { get; init; }

    public decimal? Armor { get; init; }

    public GemPowers? GemPowers { get; init; }

    public ImbuedTraits? Trait { get; init; }

    public int? AllocatedTraits { get; init; }

    public int? MaxTraits { get; init; }
}
```

`Id` publishes as a required path parameter. Every other property publishes as an optional query
parameter under its own name.

Each request model carries `ToQuery(IQueryCollection raw)`, which returns the `TooltipQuery` the
cards already read. `TooltipQuery` keeps its shape and loses `Parse` and `Parameters`.
`ApiQuerySet`, `WithQuery`, `ApiParameter` and `ApiValue` go with them, along with
`CodexQuery.Parameters` and `SuggestionService.Parameters`.

`NoSocket` is `bool?`, and the socket reads as empty when it is `true`.

### What each type accepts

| type | parameters beyond `Id` |
| --- | --- |
| item | `Hero`, `Rarity`, `ItemLevel`, `Upgrades`, `MaxUpgrades`, `NoSocket`, `PotentialPower`, `Armor`, `GemPowers`, `Trait`, `AllocatedTraits`, `MaxTraits` |
| talent-slot | `Hero` |
| blessing | `State`, `EquippedTier` |
| trait | `State` |
| gem, currency | `Rarity` |
| the remaining 16 | none |

The record endpoints for item and talent-slot carry `Hero`. Every other record endpoint takes `Id`
alone.

## The list parameters

`Trait` and `GemPowers` each bind through a type that parses a comma separated list:

```csharp
public sealed class ImbuedTraits
{
    public required IReadOnlyList<ImbuedTrait> Values { get; init; }

    public static bool TryParse(string? value, out ImbuedTraits result) { ... }
}
```

A single `TryParse` reads both `?trait=1:2,3:4` and `?trait=1:2&trait=3:4`, because repeated query
values reach the parser joined by a comma. `?trait=7` reads as trait 7 at rank 1.

`AddOpenApi` carries a schema transformer that publishes `ImbuedTraits` and `GemPowers` as
`{"type": "string"}` with an example, so the document describes the text a caller writes.

## The stat parameters

Every tooltip endpoint accepts a magnitude for any stat the build names, as `?Stamina=40`, and a
rating as a percentage with the `Pct` suffix. The names come from `store.Stats.Names`, so they are
read from the raw query rather than declared as properties.

`.WithStats()` marks the endpoints that take them. An operation transformer appends the sentence
naming the stats to each marked operation.

A query key that is neither a property of that endpoint's request model nor a stat the build names
answers 400 naming the key. The property names come from the request model's own properties.

## The browsing parameters

`SearchRequest` carries `Q`, `Type` and `Limit`. A `Type` naming no entity type answers 400 with
`PublicTypes.NoSuchType`, and `SuggestionService.Limit` keeps clamping `Limit` between one and
`SuggestionService.MaximumLimit`.

`EntitiesRequest` carries the parameters `CodexQuery` reads by name: the page, the sort, the group
and the search text.

The facets, flags and measures `/api/entities` also accepts are named by `ICodexService.Facets`,
`Flags` and `Measures`, which the build's own data fills, so they cannot be properties. An operation
transformer declares them on that one operation, along with the sentence describing how a measure
bounds a range through its `Min` and `Max` parameters. It is the only endpoint that declares
parameters this way.

## Validation and errors

`AddValidation` answers a `[Range]` breach with problem details naming the field, and publishes the
bounds into the parameter's schema.

The checks that read the build stay in the handler, where they name what the build holds: an unknown
hero names the roster, an unknown rarity names the rungs and the gem rungs, and an unknown item
trait names the id. `ITooltipService` keeps those responses as it writes them today.

## The services

Every type registered in the container lives in `Services/`, one file holding an interface and its
implementation, named for what it serves:

| today | becomes |
| --- | --- |
| `ExportPaths` | `IExportPathService`, `ExportPathService` |
| `EntityStore` | `IEntityStoreService`, `EntityStoreService` |
| `DesignTokens` | `IDesignTokenService`, `DesignTokenService` |
| `RichTextRenderer` | `IRichTextService`, `RichTextService` |
| `ChromeAssets` | `IChromeAssetService`, `ChromeAssetService` |
| `Placeholders` | `IPlaceholderService`, `PlaceholderService` |
| `TooltipView` | `ITooltipViewService`, `TooltipViewService` |
| `Fragments` | `IFragmentService`, `FragmentService` |
| `Tooltips` | `ITooltipService`, `TooltipService` |
| `Codex` | `ICodexService`, `CodexService` |
| `SearchIndex` | `ISearchIndexService`, `SearchIndexService` |
| `CodexSearch` | `ICodexSearchService`, `CodexSearchService` |
| `CodexView` | `ICodexViewService`, `CodexViewService` |
| `Suggestions` | `ISuggestionService`, `SuggestionService` |

`ExportPathService` moves out of `IconSeeder.cs` and `CodexSearchService` out of `CodexQuery.cs`, so
each file holds one service.

`IconSeederService` moves to `Services/` and stays registered through `AddHostedService`, which
resolves it as the `IHostedService` the host runs.

Components and endpoints take the interface: `@inject ICodexViewService Ui`. An interface declares
the instance members its callers read; the static members a service holds, such as
`SuggestionService.Limit` and its bounds, stay on the implementation.

`DesignTokenService` and `RichTextService` are built from `IEntityStoreService.Vocabulary`, so their
registrations stay factories and register the interface.

The constants and nested types `TooltipView` declares stay on a static `TooltipView` in `Models/`:
`Root`, `SlotWidth`, `TraitType`, `Magnitude`, `Block`, `Fact` and `TraitLine`. The members that
read the build move to `TooltipViewService`.

`CodexQuery`, `TooltipQuery`, `PublicEntity`, `PublicTypes`, `Bearings`, `BrowseView`,
`StatMagnitudes`, `StatConversion` and `Tempering` stay in `Models/`, being the shapes the services
read and write.

## Program.cs

`Program.cs` builds the host, registers the services, sets up the middleware, maps the Razor
components and calls `app.MapApiEndpoints()`. `Fragments` and `Tooltips` become `FragmentService`
and `TooltipService` under `Services/`. `public partial class Program` stays, so
`WebApplicationFactory<Program>` still finds it.

`PublicRoutes` goes. Its callers were the three routes that took a slug, and the one response still
owed, the 400 `/api/search` answers for a `type=` naming no entity type, is written in
`SearchEndpoint.cs` from `PublicTypes.NoSuchType`. `PublicTypes.NoSuchType` keeps its other callers
in `NotFound.razor` and `CodexQuery`.

`ApiDocument` keeps `Name`, `Route`, `Title`, `Ui`, `Describe` and `ApiInfo`. Tags come from
`PublicTypes.Label`, so a type added to `EntityType` names its own tag.

## Tests

- `RouteTests` walks `Enum.GetValues<EntityType>()` and asks `/api/{slug}/{id}` and
  `/api/{slug}/{id}/tooltip` for one id of every type, so a type added later is covered without the
  test being edited.
- `EndpointTests` reads `EndpointDataSource` and asserts every `EntityType` member has both routes
  registered, which fails when a type is added without its endpoint files.
- `ParameterTests` reads the published OpenAPI document and asserts that `/api/ability/{id}/tooltip`
  and `/api/item/{id}/tooltip` declare no parameter in common beyond `id`, that every declared
  parameter binds a property of that endpoint's request model, and that `id` is the only required
  parameter on any route.
- `ApiDocumentTests` reads the published document rather than the `ApiParameter` lists.
- `RouteTests.An_entity_page_carries_the_hero_and_the_state_into_its_refetch_address` asserts the
  `data-stage` address reads `/api/item/{id}/tooltip?hero=...&state=...`.
- `TooltipQueryTests` covers `ToQuery` on each request model that carries one, and the comma and
  repetition forms of `Trait` and `GemPowers`.
- `TooltipTests`, `SheetTests`, `StateTests`, `InsetArtTests`, `BlessingCardTests` and
  `TraitCardTests` render through the components and pass unchanged.

## Verification

`TooltipDumpTests` writes one file per case to the folder `FELLOWSHIP_TIP_DUMP` names. Run it before
the move and after, and diff the two folders. The endpoints change how a request is bound and where
a route is declared, not what a card draws, so an empty diff is what says the move preserved the
rendering.

Ask the running app for `/openapi/v1.json` and read the item and ability tooltip operations. The
item operation carries its twelve parameters, the ability operation carries `id` alone, and neither
carries a parameter the other does.
