using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class AspectRecordEndpoint
{
    public static RouteHandlerBuilder MapAspectRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAspect")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Aspect)} by ID.")
            .Produces<AspectRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AugmentationRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<AspectRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
