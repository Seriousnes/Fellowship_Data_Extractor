using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record RelicPowerTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error);
}

public static class RelicPowerTooltipEndpoint
{
    public static RouteHandlerBuilder MapRelicPowerTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetRelicPowerTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.RelicPower)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] RelicPowerTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.RelicPower, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
