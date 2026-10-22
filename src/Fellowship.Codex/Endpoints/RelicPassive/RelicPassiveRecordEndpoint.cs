using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record RelicPassiveRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class RelicPassiveRecordEndpoint
{
    public static RouteHandlerBuilder MapRelicPassiveRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetRelicPassive")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.RelicPassive)} by ID.")
            .Produces<RelicPassiveRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] RelicPassiveRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<RelicPassiveRecord>(request.Id, null, http, store, view);
}
