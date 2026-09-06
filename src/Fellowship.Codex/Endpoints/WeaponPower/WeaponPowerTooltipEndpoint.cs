using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class WeaponPowerTooltipEndpoint
{
    public static RouteHandlerBuilder MapWeaponPowerTooltip(this IEndpointRouteBuilder group) =>
        group.MapMethods("/{id:int}/tooltip", [HttpMethods.Query], (
                [Description("The id to draw.")] int id,
                WeaponPowerTooltipRequest? request,
                HttpContext http,
                ITooltipService tooltips,
                IEntityStoreService store) =>
            Handle(id, request, http, tooltips, store))
            .WithName("QueryWeaponPowerTooltip")
            .WithSummary(
                $"One {PublicTypes.Noun(EntityType.WeaponPower)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Accepts<WeaponPowerTooltipRequest>("application/json")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        int id,
        WeaponPowerTooltipRequest? request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = TooltipRequests.Read(request, store, out var error);

        return error is null
            ? tooltips.Get(EntityType.WeaponPower, id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
