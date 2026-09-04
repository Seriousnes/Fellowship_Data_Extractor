using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record CreatureTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    [Description("Given as true when the entity's art is to be drawn beside the card. A card that draws its art inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description("Given as true, or bare, when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button and a link to its page.")]
    public Switch? Mobile { get; init; }

    [Description("The dungeon difficulty to read the creature's health at.")]
    public int? Difficulty { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with
        {
            ShowIcon = ShowIcon == true,
            Mobile = Mobile?.On == true,
            Difficulty = Difficulty,
        };
}

public static class CreatureTooltipEndpoint
{
    public static RouteHandlerBuilder MapCreatureTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetCreatureTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Creature)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] CreatureTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.Creature, request.Id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
