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

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

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

app.Run();

/// <summary>
/// What both endpoints do, which is the same thing against a different table: read the query, find
/// the one entity the caller means, and render it.
/// </summary>
public sealed class Tooltips(EntityStore store, TooltipRenderer renderer)
{
    public IResult Get(string table, int netId, IQueryCollection request)
    {
        var query = TooltipQuery.Parse(
            request, store.KnownStats, store.Vocabulary.StatCalculation.Attributes, out var error);

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
