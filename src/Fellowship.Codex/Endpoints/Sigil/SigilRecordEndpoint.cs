using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class SigilRecordEndpoint
{
    public static RouteHandlerBuilder MapSigilRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSigil")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Sigil)} by ID.")
            .Produces<SigilRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AugmentationRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<SigilRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
