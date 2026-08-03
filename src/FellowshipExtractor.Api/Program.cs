using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.AddAzureBlobContainerClient("icons");
builder.Services.AddSingleton<ExportPaths>();
builder.Services.AddHostedService<IconSeeder>();

builder.Services.AddSingleton<EntityStore>();
builder.Services.AddSingleton(s => new DesignTokens(
    s.GetRequiredService<EntityStore>().Vocabulary.Colors,
    s.GetRequiredService<EntityStore>().Vocabulary.TextStyles));
builder.Services.AddSingleton(s => new RichTextRenderer(
    s.GetRequiredService<EntityStore>().Vocabulary.TextStyles));

builder.Services.AddSingleton<ChromeAssets>();
builder.Services.AddSingleton<Placeholders>();
builder.Services.AddSingleton<TooltipRenderer>();
builder.Services.AddSingleton<Tooltips>();

builder.Services.AddSingleton<Codex>();
builder.Services.AddSingleton<SearchIndex>();
builder.Services.AddSingleton<CodexSearch>();
builder.Services.AddSingleton<CodexView>();
builder.Services.AddSingleton<Suggestions>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseResponseCompression();
app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/api/ability/{abilityId:int}", (int abilityId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get("GameplayAbility", abilityId, http.Request.Query))
    .WithName("GetAbilityTooltip")
    .WithSummary("One ability's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/item/{itemId:int}", (int itemId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get("ItemID", itemId, http.Request.Query))
    .WithName("GetItemTooltip")
    .WithSummary("One item's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/tooltip/{table}/{netId:int}", (string table, int netId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get(table, netId, http.Request.Query))
    .WithName("GetTooltip")
    .WithSummary("Any entity's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/search", (HttpContext http, Suggestions suggestions) =>
{
    var limit = int.TryParse(http.Request.Query["limit"], out var wanted) ? Math.Clamp(wanted, 1, 100) : 10;

    return Results.Ok(suggestions.For(
        http.Request.Query["q"].ToString(), http.Request.Query["type"].ToString(), limit));
})
    .WithName("Search")
    .WithSummary("Ranked name matches across every entity type.");

app.MapGet("/api/entities", (HttpContext http, Codex codex, CodexSearch search, CodexView view) =>
{
    var query = CodexQuery.Parse(http.Request.Query, codex, out var error);

    if (error is not null)
    {
        return Results.Problem(
            title: "The filter could not be read.", detail: error, statusCode: StatusCodes.Status400BadRequest);
    }

    var results = search.Run(query);

    return Results.Ok(new EntitiesResponse(
        results.Total,
        results.Page,
        results.Pages,
        [.. results.Rows.Select(row => SearchResult.From(row, view))]));
})
    .WithName("GetEntities")
    .WithSummary("Every entity a filter leaves, paged.");

app.MapGet("/api/entity/{table}/{netId:int}", (string table, int netId, EntityStore store) =>
{
    var readings = store.All(table, netId);

    return readings.Count == 0
        ? Results.Problem(
            title: "No such id.", detail: $"The build has no {table} {netId}.",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(readings);
})
    .WithName("GetEntity")
    .WithSummary("One entity's exported record, with every per-hero reading of its id.");

app.MapGet("/api/catalog", (Codex codex, EntityStore store) => Results.Ok(new
{
    entities = store.Count,
    types = codex.Types,
    menu = codex.Menu,
    facets = codex.Facets.Select(facet => codex.View(facet, store.Entities)),
    flags = codex.Flags.Select(flag => new { flag.Key, flag.Label }),
    measures = codex.Measures.Select(measure => new { measure.Key, measure.Label, measure.Unit }),
    store.Vocabulary.Heroes,
    store.Vocabulary.Rarities,
}))
    .WithName("GetCatalog")
    .WithSummary("The menus, entity types and filter facets the browser is driven by.");

app.Run();

public sealed class Tooltips(EntityStore store, TooltipRenderer renderer)
{
    public IResult Get(string table, int netId, IQueryCollection request)
    {
        var query = TooltipQuery.Parse(request, store.KnownStats, out var error);

        if (error is not null)
        {
            return Results.Problem(
                title: "The request parameters could not be read.",
                detail: $"{error} Stat parameters are {string.Join(", ", store.KnownStats.Order())}.",
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

        var outcome = store.Resolve(table, netId, query.Hero, out var entity, out var hero);

        return outcome switch
        {
            Lookup.Found => Results.Content(renderer.Render(entity!, query, hero), "text/html; charset=utf-8"),

            Lookup.NoSuchId => Results.Problem(
                title: "No such id.",
                detail: $"The build has no {table} {netId}.",
                statusCode: StatusCodes.Status404NotFound),

            Lookup.NeedsHero => Results.Problem(
                title: "This id reads differently per hero.",
                detail: $"{table} {netId} has {store.All(table, netId).Count} readings and no hero was given. " +
                    "Add hero=<name> to pick one. The readings are " +
                    string.Join("; ", store.All(table, netId).Select(Describe)) + ".",
                statusCode: StatusCodes.Status400BadRequest),

            Lookup.NotForThatHero => Results.Problem(
                title: "Not an item for that hero.",
                detail: $"{table} {netId} exists but has no reading for {query.Hero}.",
                statusCode: StatusCodes.Status404NotFound),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static string Describe(FellowshipExtractor.Tooltip.Entity entity) =>
        $"{entity.DisplayName ?? entity.Name} [{entity.ArmorType ?? "any"}/{entity.PrimaryStat ?? "any"}]";
}
