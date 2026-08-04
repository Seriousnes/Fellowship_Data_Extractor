using FellowshipExtractor.Api;
using FellowshipExtractor.Api.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// A field the row does not carry is left out rather than written as null, which is how the export
// itself reads. Entities differ field by field - an item has stats, an ability has a cast time - so
// a row written in full would be mostly nulls, and a reader could not see what it does say.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition =
        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.AddAzureBlobContainerClient("icons");
builder.Services.AddSingleton<ExportPaths>();
builder.Services.AddHostedService<IconSeeder>();

builder.Services.AddSingleton<EntityStore>();
builder.Services.AddSingleton(s => new DesignTokens(s.GetRequiredService<EntityStore>().Vocabulary));
builder.Services.AddSingleton(s => new RichTextRenderer(
    s.GetRequiredService<EntityStore>().Vocabulary.TextStyles));

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
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/api/ability/{abilityId:int}", (int abilityId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get("ability", abilityId, http.Request.Query))
    .WithName("GetAbilityTooltip")
    .WithSummary("One ability's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/item/{itemId:int}", (int itemId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get("items", itemId, http.Request.Query))
    .WithName("GetItemTooltip")
    .WithSummary("One item's tooltip as a self-contained HTML fragment.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/tooltip/{slug}/{netId:int}", (string slug, int netId, HttpContext http, Tooltips tooltips) =>
        tooltips.Get(slug, netId, http.Request.Query))
    .WithName("GetTooltip")
    .WithSummary("Any entity's tooltip as a self-contained HTML fragment, addressed by type and id.")
    .Produces<string>(StatusCodes.Status200OK, "text/html");

app.MapGet("/api/search", (HttpContext http, Suggestions suggestions) =>
{
    var limit = int.TryParse(http.Request.Query["limit"], out var wanted) ? Math.Clamp(wanted, 1, 100) : 10;

    // A type that names nothing is answered rather than ignored. Narrowing to it silently would
    // return no matches, which reads as "the build has none of those" instead of "there is no such
    // type", and a caller cannot tell those two apart from an empty list.
    var asked = http.Request.Query["type"].ToString();
    var narrowed = string.IsNullOrWhiteSpace(asked) ? null : asked;

    if (narrowed is not null && PublicTypes.TableOf(narrowed) is null)
    {
        return PublicRoutes.NoSuchType(narrowed, StatusCodes.Status400BadRequest);
    }

    // The slug is handed on as the slug. Resolving it here and passing the table would type-check
    // and answer nothing at all: the narrowing resolves its own argument, so a table name arriving
    // there names no type and empties every result.
    return Results.Ok(suggestions.For(http.Request.Query["q"].ToString(), narrowed, limit));
})
    .WithName("Search")
    .WithSummary("Ranked name matches, across every type or the one the type= slug names.");

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

app.MapGet("/api/entity/{slug}/{netId:int}", (string slug, int netId, EntityStore store, CodexView view) =>
{
    if (PublicTypes.TableOf(slug) is not { } table)
    {
        return PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound);
    }

    var readings = store.All(table, netId);

    // Projected, never handed over as it was read. The exported record is the extractor's reading of
    // the row and carries the build's own name for it, the tuning rows behind its numbers and the
    // tags it is filed under; this endpoint is a page a reader can open, so it answers with the
    // reader's reading of the same row.
    return readings.Count == 0
        ? Results.Problem(
            title: "No such id.", detail: $"The build has no {PublicTypes.Noun(table)} {netId}.",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(readings.Select(reading => PublicEntity.From(reading, view)));
})
    .WithName("GetEntity")
    .WithSummary("One entity's record, with every per-hero reading of its id.");

app.MapGet("/api/catalog", (Codex codex, EntityStore store) =>
{
    // Faceted over the public rows alone. A facet counted over the whole export offers every value
    // the other 757 rows carry, so a menu built from it grows entries no type tab leads to and a
    // reader meets an internal spelling through the filter panel rather than through a page.
    var rows = store.Entities.Where(entity => PublicTypes.IsPublic(entity.Table)).ToArray();

    return Results.Ok(new
    {
        // The same number the footer of every page prints, read from the one place it is worked out,
        // so the corpus a reader is told about is the corpus they can reach whichever surface tells
        // them. Two counts of the same thing is how the footer came to promise 757 rows nothing here
        // answers with.
        entities = codex.Count,

        // The types as a caller addresses them. EntityType carries the table so code can match a
        // row against it, and a record serialises every member it has, so writing the list straight
        // out published all 7 shipped table names on the one response a browser fetches first.
        types = codex.Types.Select(type => new { type.Slug, type.Label, type.Group, type.Count }),
        menu = codex.Menu,
        facets = codex.Facets.Select(facet => codex.View(facet, rows)),
        flags = codex.Flags.Select(flag => new { flag.Key, flag.Label }),
        measures = codex.Measures.Select(measure => new { measure.Key, measure.Label, measure.Unit }),

        // The roster and the ladder as the game prints them. Both records carry a column the game
        // never shows: a hero's codename is the asset spelling their files are named by, and a
        // rarity's filed name is the word tier 4 is stored under while an item calls it Heroic and a
        // gem calls it Flawless. This response is a page a reader can open, so neither is written.
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
    .WithSummary("The menus, entity types and filter facets the browser is driven by.");

app.Run();

/// <summary>
/// The answer an address gives when the type it names is not one a reader can be addressed to.
///
/// The sentence itself is <see cref="PublicTypes.NoSuchType"/>, which the not-found page says too;
/// this wraps it as a problem response. Only the status is decided here, and it differs by where
/// the caller wrote the type: a path that names nothing is a page that does not exist, and a query
/// value that names nothing is a request that cannot be answered.
/// </summary>
public static class PublicRoutes
{
    public static IResult NoSuchType(string? asked, int status) => Results.Problem(
        title: "No such type.",
        detail: PublicTypes.NoSuchType(asked),
        statusCode: status);
}

/// <summary>
/// A component rendered to a string, for the callers who ask for markup rather than for a page.
///
/// The same component the pages place, so a tooltip a caller fetches and a tooltip the entity page
/// draws cannot come to disagree: there is one definition of what a tooltip is, and this is the
/// path to it for readers who have no browser.
/// </summary>
public sealed class Fragments(IServiceScopeFactory scopes, ILoggerFactory loggers)
{
    public async Task<string> Render<T>(Dictionary<string, object?> parameters)
        where T : Microsoft.AspNetCore.Components.IComponent
    {
        // A scope of its own, because the renderer resolves scoped services of the component model's
        // and the root provider refuses to hand those out.
        await using var scope = scopes.CreateAsyncScope();

        await using var renderer = new Microsoft.AspNetCore.Components.Web.HtmlRenderer(
            scope.ServiceProvider, loggers);

        // The renderer has a dispatcher of its own and every render has to run on it, the same way a
        // component's lifecycle does anywhere else.
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<T>(
                Microsoft.AspNetCore.Components.ParameterView.FromDictionary(parameters));

            return rendered.ToHtmlString();
        });
    }
}

public sealed class Tooltips(EntityStore store, Fragments fragments)
{
    /// <summary>
    /// One tooltip, for the type a slug names and the id beside it.
    ///
    /// The slug is resolved before anything else is read, because an address that names no type is
    /// wrong in a way no parameter can fix. It is also the only spelling accepted: the table a row
    /// lives in is an internal fact, so a caller who sends one is sending a type this app does not
    /// have and is answered the same as any other misspelling.
    /// </summary>
    public async Task<IResult> Get(string slug, int netId, IQueryCollection request)
    {
        if (PublicTypes.TableOf(slug) is not { } table)
        {
            return PublicRoutes.NoSuchType(slug, StatusCodes.Status404NotFound);
        }

        var query = TooltipQuery.Parse(request, store.Stats, out var error);

        if (error is not null)
        {
            // The stats by the names the game prints for them, which are also the only names this
            // accepts. The sentence is where a caller who mistyped one finds the right spelling, so
            // listing the build's own would teach them an address that no longer resolves.
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

        var outcome = store.Resolve(table, netId, query.Hero, out var entity, out var hero);

        // What the caller asked for, in the words they asked in: the type they addressed and the id
        // they wrote, never the table the row turned out to live in. The readings are read once here
        // rather than twice inside the sentence that needs them.
        var noun = PublicTypes.Noun(table);
        var readings = store.All(table, netId);

        if (outcome == Lookup.Found)
        {
            var fragment = await fragments.Render<FellowshipExtractor.Api.Components.Tooltip.Tooltip>(new()
            {
                ["Entity"] = entity!,
                ["Query"] = query,
                ["Hero"] = hero,
            });

            return Results.Content(fragment, "text/html; charset=utf-8");
        }

        return outcome switch
        {
            Lookup.NoSuchId => Results.Problem(
                title: "No such id.",
                detail: $"The build has no {noun} {netId}.",
                statusCode: StatusCodes.Status404NotFound),

            // The sentence is built around the count rather than opening on the thing, because the
            // noun is lower case and the type it names has no capitalised spelling that is not the
            // internal one.
            Lookup.NeedsHero => Results.Problem(
                title: "This id reads differently per hero.",
                detail: $"The build has {readings.Count} readings of {noun} {netId} and no hero was given. " +
                    "Add hero=<name> to pick one. The readings are " +
                    string.Join("; ", readings.Select(Describe)) + ".",
                statusCode: StatusCodes.Status400BadRequest),

            // The hero is named back by the name the game prints rather than by whatever the caller
            // typed, because a codename resolves as readily as a name and echoing one would put an
            // asset spelling in the reply. The hero is known here: an unknown one was turned away
            // above, so this outcome only arises for a hero the roster has.
            Lookup.NotForThatHero => Results.Problem(
                title: "Not for that hero.",
                detail: $"The build has no reading of {noun} {netId} for {hero!.Name}.",
                statusCode: StatusCodes.Status404NotFound),

            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    /// <summary>
    /// One reading of an ambiguous id, as the sentence offering a choice between them writes it. The
    /// name is the reader's name for the row and never its gameplay tag, and the pair beside it is
    /// the two things the readings of an id differ by.
    /// </summary>
    private static string Describe(FellowshipExtractor.Tooltip.Entity entity) =>
        $"{PublicTypes.NameOf(entity)} [{entity.ArmorType ?? "any"}/{entity.PrimaryStat ?? "any"}]";
}
