using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record BlessingRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class BlessingRecordEndpoint
{
    public static RouteHandlerBuilder MapBlessingRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetBlessing")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Blessing)} by ID.")
            .Produces<BlessingRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] BlessingRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<BlessingRecord>(request.Id, null, http, store, view);
}
