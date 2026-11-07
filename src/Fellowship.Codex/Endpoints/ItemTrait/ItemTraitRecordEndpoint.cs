using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class ItemTraitRecordEndpoint
{
    public static RouteHandlerBuilder MapItemTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetItemTrait")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.ItemTrait)} by ID.")
            .Produces<ItemTraitRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AugmentationRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<ItemTraitRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
