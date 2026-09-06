using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class GemTraitTooltipEndpoint
{
    public static RouteHandlerBuilder MapGemTraitTooltip(this IEndpointRouteBuilder group) =>
        group.MapMethods("/{id:int}/tooltip", [HttpMethods.Query], (
                [Description("The id to draw.")] int id,
                GemTraitTooltipRequest? request,
                HttpContext http,
                ITooltipService tooltips,
                IEntityStoreService store) =>
            Handle(id, request, http, tooltips, store))
            .WithName("QueryGemTraitTooltip")
            .WithSummary(
                $"One {PublicTypes.Noun(EntityType.GemTrait)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Accepts<GemTraitTooltipRequest>("application/json")
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        int id,
        GemTraitTooltipRequest? request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = TooltipRequests.Read(request, out var error);

        return error is null
            ? tooltips.Get(EntityType.GemTrait, id, query, Origins.Of(http))
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
