using FellowshipExtractor.Api;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// The blob container the AppHost provisions, and the export on disk it is filled from.
builder.AddAzureBlobContainerClient("icons");
builder.Services.AddSingleton<ExportPaths>();
builder.Services.AddHostedService<IconSeeder>();

// The export is read once and shared. Everything downstream of it is stateless.
builder.Services.AddSingleton<EntityStore>();
builder.Services.AddSingleton(s => new DesignTokens(
    s.GetRequiredService<EntityStore>().Vocabulary.Colors,
    s.GetRequiredService<EntityStore>().Vocabulary.TextStyles));
builder.Services.AddSingleton(s => new RichTextRenderer(
    s.GetRequiredService<EntityStore>().Vocabulary.TextStyles));

// Where the tooltip's chrome lives in the container, including the per-rarity tinted masks the
// seeder above generates. Anything that needs one of those URLs asks this rather than spelling the
// naming rule out again.
builder.Services.AddSingleton<ChromeAssets>();
builder.Services.AddSingleton<Placeholders>();
builder.Services.AddSingleton<TooltipRenderer>();
builder.Services.AddSingleton<Tooltips>();

// The codex: the menus, facets and name index the browser is driven by, all counted off the same
// export the tooltips are rendered from, so a page can never offer a filter the tooltips disagree
// with.
builder.Services.AddSingleton<Codex>();
builder.Services.AddSingleton<SearchIndex>();
builder.Services.AddSingleton<CodexSearch>();
builder.Services.AddSingleton<CodexView>();

builder.Services.AddRazorPages();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseResponseCompression();
app.UseStaticFiles();

app.MapRazorPages();

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

// Every other table renders through the same code; the two routes above are the two a caller asks
// for by name often enough to be worth their own address. The browser's detail page fetches this
// one, so a reader changing the rung or the hero re-renders the real tooltip rather than a copy of
// it drawn by the page.
app.MapGet("/api/tooltip/{table}/{netId:int}", (string table, int netId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get(table, netId, http.Request.Query))
    .WithName("GetTooltip")
    .WithSummary("Any entity's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

// What the omnibox reads. Ranked by the index, grouped by table so the type chips above the results
// carry counts, and capped: a reader typing one letter reaches thousands of rows and wants ten.
app.MapGet("/api/search", (HttpContext http, SearchIndex index, CodexView view) =>
{
    var text = http.Request.Query["q"].ToString();
    if (string.IsNullOrWhiteSpace(text)) return Results.Ok(SearchResponse.Empty);

    var limit = int.TryParse(http.Request.Query["limit"], out var wanted) ? Math.Clamp(wanted, 1, 100) : 10;
    var table = http.Request.Query["type"].ToString();

    var hits = index.Search(text, 2000);

    var groups = hits
        .GroupBy(hit => hit.Entity.Table!, StringComparer.Ordinal)
        .Select(group => new SearchGroup(group.Key, Codex.Label(group.Key), group.Count()))
        .OrderByDescending(group => group.Count)
        .ToArray();

    var chosen = string.IsNullOrWhiteSpace(table)
        ? hits
        : [.. hits.Where(hit => string.Equals(hit.Entity.Table, table, StringComparison.OrdinalIgnoreCase))];

    return Results.Ok(new SearchResponse(
        text,
        hits.Count,
        groups,
        [.. chosen.Take(limit).Select(hit => SearchResult.From(hit.Entity, view))]));
})
    .WithName("Search")
    .WithSummary("Ranked name matches across every entity type.");

// The filtered corpus as data. The browser's own pages render server side and do not call this; it
// is here because every filter the pages offer should be answerable without them.
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

// One entity as the export holds it, every reading of the id included. Which reading a reader means
// is not always decidable from the id, so all of them are answered and the page picks.
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

// The vocabulary the browser's menus and filter panel are built from, counted off the export.
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

/// <summary>
/// One entity as a list answers with it: enough to draw a row and link to the page. The icon arrives
/// as the URL it is fetched from rather than as the file name the export writes, because a caller
/// drawing the row has no way to work the container address out for itself.
/// </summary>
public sealed record SearchResult(
    string Table,
    string TypeLabel,
    int NetId,
    string Name,
    string? DisplayName,
    string? Icon,
    string IconUrl,
    string? Slot,
    string? ArmorType,
    IReadOnlyList<string>? Heroes)
{
    public static SearchResult From(FellowshipExtractor.Tooltip.Entity entity, CodexView view) => new(
        entity.Table!,
        Codex.Label(entity.Table!),
        entity.NetId ?? 0,
        entity.Name ?? string.Empty,
        entity.DisplayName,
        entity.Icon,
        view.Icon(entity.Icon),
        entity.Slot,
        entity.ArmorType,
        entity.Heroes);
}

public sealed record SearchGroup(string Table, string Label, int Count);

public sealed record SearchResponse(
    string Query, int Total, IReadOnlyList<SearchGroup> Groups, IReadOnlyList<SearchResult> Results)
{
    public static readonly SearchResponse Empty = new(string.Empty, 0, [], []);
}

public sealed record EntitiesResponse(int Total, int Page, int Pages, IReadOnlyList<SearchResult> Rows);

/// <summary>
/// What both tooltip endpoints do, which is the same thing against a different table: read the query,
/// find the one entity the caller means, and render it.
/// </summary>
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

        // A rung a caller misspells has to be rejected rather than dropped. The rung decides how
        // many stat slots the item's rarity grants it, so a tooltip rendered from an unread rarity
        // is short a stat line or two and says nothing about why.
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

            // The id is real and names several things at once. Answering with one of them would be
            // a guess, and the caller cannot tell a guess from an answer, so the readings are listed
            // and the discriminator that settles them is named.
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
