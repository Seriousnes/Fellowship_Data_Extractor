using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class Cards
{
    public const string Prefix = "og";

    private const string Version = "v";

    private const string Hero = "hero";

    public static string Address(ICodexViewService ui, EntityDocument document, string? search, string? hero, string build)
    {
        var query = new Dictionary<string, StringValues>(QueryHelpers.ParseQuery(search), StringComparer.OrdinalIgnoreCase);

        query.Remove(Hero);
        if (hero is not null) query[Hero] = hero;
        query[Version] = build;

        return QueryHelpers.AddQueryString($"/{Prefix}{ui.Path(document)}.png", query);
    }

    public static IEndpointRouteBuilder MapCards(this IEndpointRouteBuilder app)
    {
        app.MapGet($"/{Prefix}/{{slug}}/{{id:int}}/{{named}}.png", Get)
            .Serve()
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> Get(
        string slug,
        int id,
        string named,
        HttpContext http,
        IEntityStoreService store,
        ICardService cards,
        CancellationToken cancellationToken)
    {
        if (!EntityTypes.TryParse(slug, out var type) || !PublicTypes.IsListed(type) ||
            store.All(type, id) is not { Count: > 0 } readings)
        {
            return Results.NotFound();
        }

        var raw = QueryHelpers.ParseQuery(http.Request.QueryString.Value);
        raw.Remove(Version);

        var hero = raw.TryGetValue(Hero, out var given) ? StatQuery.NullIfEmpty(given.FirstOrDefault()) : null;
        var wearer = store.Hero(hero);

        var document = (wearer is null ? null : readings.FirstOrDefault(reading => EntityStore.Reads(reading, wearer)))
            ?? readings.FirstOrDefault(reading => PublicTypes.Segment(reading) == named)
            ?? readings[0];

        var card = await cards.Card(document, PageQuery.Read(raw, store, hero, document));

        return Results.File(await cards.Render(card, cancellationToken), "image/png");
    }
}
