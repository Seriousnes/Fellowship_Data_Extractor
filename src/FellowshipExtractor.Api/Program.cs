using Fellowship.SDK.Documents;

using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Components;
using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi(ApiDocument.Name, options =>
{
    options.AddDocumentTransformer<ApiInfo>();
    options.AddOperationTransformer<ApiParameters>();
});
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.AddAzureBlobContainerClient("icons");
builder.Services.AddSingleton<IExportPathService, ExportPathService>();
builder.Services.AddHostedService<IconSeederService>();

builder.Services.AddSingleton<IEntityStoreService, EntityStoreService>();
builder.Services.AddSingleton(s => new DesignTokens(s.GetRequiredService<IEntityStoreService>().Vocabulary));
builder.Services.AddSingleton(s => new RichTextRenderer(
    s.GetRequiredService<IEntityStoreService>().Vocabulary.TextStyles));

builder.Services.AddSingleton<ChromeAssets>();
builder.Services.AddSingleton<Placeholders>();
builder.Services.AddSingleton<TooltipView>();
builder.Services.AddSingleton<Fragments>();
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

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(ApiDocument.Route, ApiDocument.Title);
    options.RoutePrefix = ApiDocument.Ui;
    options.DocumentTitle = ApiDocument.Title;
});

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/api/ability/{abilityId:int}", (int abilityId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get(EntityType.Ability, abilityId, http.Request.Query))
    .WithName("GetAbilityTooltip")
    .WithTags(ApiDocument.Tooltips)
    .WithSummary("One ability's tooltip as a self-contained HTML fragment.")
    .WithQuery(ApiQuerySet.Tooltip)
    .Produces<string>(StatusCodes.Status200OK, "text/html")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/item/{itemId:int}", (int itemId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get(EntityType.Item, itemId, http.Request.Query))
    .WithName("GetItemTooltip")
    .WithTags(ApiDocument.Tooltips)
    .WithSummary("One item's tooltip as a self-contained HTML fragment.")
    .WithQuery(ApiQuerySet.Tooltip)
    .Produces<string>(StatusCodes.Status200OK, "text/html")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/tooltip/{slug}/{id:int}", async (string slug, int id, HttpContext http, Tooltips tooltips) =>
        EntityTypes.TryParse(slug, out var type)
            ? await tooltips.Get(type, id, http.Request.Query)
            : PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound))
    .WithName("GetTooltip")
    .WithTags(ApiDocument.Tooltips)
    .WithSummary("Any entity's tooltip as a self-contained HTML fragment, addressed by type and id.")
    .WithQuery(ApiQuerySet.Tooltip)
    .Produces<string>(StatusCodes.Status200OK, "text/html")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/search", (HttpContext http, Suggestions suggestions) =>
{
    var limit = Suggestions.Limit(http.Request.Query["limit"]);

    var asked = http.Request.Query["type"].ToString();
    var narrowed = string.IsNullOrWhiteSpace(asked) ? null : asked;

    if (narrowed is not null && !EntityTypes.TryParse(narrowed, out _))
    {
        return PublicRoutes.NoSuchType(narrowed, StatusCodes.Status400BadRequest);
    }

    return Results.Ok(suggestions.For(http.Request.Query["q"].ToString(), narrowed, limit));
})
    .WithName("Search")
    .WithTags(ApiDocument.Browsing)
    .WithSummary("Ranked name matches, across every type or the one the type= slug names.")
    .WithQuery(ApiQuerySet.Search)
    .Produces<SearchResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

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
        [.. results.Rows.Select(row => SearchResult.From(row.Shown, view))]));
})
    .WithName("GetEntities")
    .WithTags(ApiDocument.Browsing)
    .WithSummary("Every entity a filter leaves, paged.")
    .WithQuery(ApiQuerySet.Catalogue)
    .Produces<EntitiesResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/api/entity/{slug}/{id:int}", (string slug, int id, IEntityStoreService store, CodexView view) =>
{
    if (!EntityTypes.TryParse(slug, out var type))
    {
        return PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound);
    }

    var readings = store.All(type, id);

    return readings.Count == 0
        ? Results.Problem(
            title: "No such id.", detail: $"The build has no {PublicTypes.Noun(type)} {id}.",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(readings.Select(reading => PublicEntity.From(reading, view)));
})
    .WithName("GetEntity")
    .WithTags(ApiDocument.Records)
    .WithSummary("One entity's record, with every per-hero reading of its id.")
    .Produces<IReadOnlyList<PublicEntity>>()
    .ProducesProblem(StatusCodes.Status404NotFound);

app.MapGet("/api/catalog", (Codex codex, IEntityStoreService store) =>
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

        heroes = store.Vocabulary.Heroes.Select(hero => new
        {
            hero.Name,
            hero.ArmorType,
            hero.PrimaryStat,
            hero.Color,
        }),
        rarities = store.Vocabulary.Rarities.Select(rarity => new
        {
            rarity.Tier,
            rarity.DisplayName,
            rarity.GemDisplayName,
        }),
    });
})
    .WithName("GetCatalog")
    .WithTags(ApiDocument.Browsing)
    .WithSummary("The menus, entity types and filter facets the browser is driven by.");

app.Run();

public partial class Program;

public static class PublicRoutes
{
    public static IResult NoSuchType(string? asked, int status) => Results.Problem(
        title: "No such type.",
        detail: PublicTypes.NoSuchType(asked),
        statusCode: status);
}

public sealed class Fragments(IServiceScopeFactory scopes, ILoggerFactory loggers)
{
    public async Task<string> Render<T>(Dictionary<string, object?> parameters)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        await using var scope = scopes.CreateAsyncScope();

        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(
            scope.ServiceProvider, loggers);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<T>(
                Microsoft.AspNetCore.Components.ParameterView.FromDictionary(parameters));

            return rendered.ToHtmlString();
        });
    }
}

public sealed class Tooltips(IEntityStoreService store, Fragments fragments)
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
            var fragment = await fragments.Render<FellowshipExtractor.Api.Components.Tooltip.Tooltip>(new()
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
