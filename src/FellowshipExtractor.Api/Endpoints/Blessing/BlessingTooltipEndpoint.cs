using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record BlessingTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    [Description("The state to draw the row at: allocated or unallocated.")]
    public string? State { get; init; }

    [Description("Which of a blessing's tiers is equipped. None given reads as none equipped.")]
    public int? EquippedTier { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with
        {
            State = StatQuery.Given(State),
            EquippedTier = EquippedTier,
        };
}

public static class BlessingTooltipEndpoint
{
    public static RouteHandlerBuilder MapBlessingTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetBlessingTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Blessing)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] BlessingTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.Blessing, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
