using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class ProtectiveEnchantRecordEndpoint
{
    public static RouteHandlerBuilder MapProtectiveEnchantRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetProtectiveEnchant")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.ProtectiveEnchant)} by ID.")
            .Produces<ProtectiveEnchantRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AugmentationRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<ProtectiveEnchantRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
