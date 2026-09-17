using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class SetBonusTooltipEndpoint
{
    public static RouteHandlerBuilder MapSetBonusTooltip(this IEndpointRouteBuilder group) =>
        group.MapMethods("/{id:int}/tooltip", [HttpMethods.Query], (
                [Description("The id.")] int id,
                SetBonusTooltipRequest? request,
                HttpContext http,
                ITooltipService tooltips,
                IEntityStoreService store) =>
            Handle(id, request, http, tooltips, store))
            .WithName("QuerySetBonusTooltip")
            .WithSummary(
                $"Self-contained HTML tooltip fragment for one {PublicTypes.Noun(EntityType.SetBonus)}.")
            .WithStats()
            .Accepts<SetBonusTooltipRequest>("application/json")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        int id,
        SetBonusTooltipRequest? request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = TooltipRequests.Read(request, out var error);

        return error is null
            ? tooltips.Get(EntityType.SetBonus, id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
