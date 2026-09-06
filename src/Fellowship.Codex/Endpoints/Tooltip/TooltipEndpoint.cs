using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class TooltipEndpoint
{
    public static RouteHandlerBuilder MapTooltip(this IEndpointRouteBuilder group, EntityType type) =>
        group.MapMethods("/{id:int}/tooltip", [HttpMethods.Query], (
                [Description("The id to draw.")] int id,
                TooltipRequest? request,
                HttpContext http,
                ITooltipService tooltips,
                IEntityStoreService store) =>
            Handle(type, id, request, http, tooltips, store))
            .WithName($"Query{type}Tooltip")
            .WithSummary($"One {PublicTypes.Noun(type)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Accepts<TooltipRequest>("application/json")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        EntityType type,
        int id,
        TooltipRequest? request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = TooltipRequests.Read(request, store, out var error);

        return error is null
            ? tooltips.Get(type, id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
