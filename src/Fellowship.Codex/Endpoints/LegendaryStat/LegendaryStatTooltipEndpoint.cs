using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record LegendaryStatTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    [Description("Given as true when the entity's art is to be drawn beside the card. A card that draws its art inside ignores it.")]
    public bool? ShowIcon { get; init; }

    [Description("Given as true, or bare, when the card is to come wrapped in the chrome a phone draws it in: a scrim, a close button and a link to its page.")]
    public Switch? Mobile { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with
        {
            ShowIcon = ShowIcon == true,
            Mobile = Mobile?.On == true,
        };
}

public static class LegendaryStatTooltipEndpoint
{
    public static RouteHandlerBuilder MapLegendaryStatTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetLegendaryStatTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.LegendaryStat)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] LegendaryStatTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.LegendaryStat, request.Id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
