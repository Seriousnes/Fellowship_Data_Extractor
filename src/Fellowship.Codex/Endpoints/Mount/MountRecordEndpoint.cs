using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record MountRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class MountRecordEndpoint
{
    public static RouteHandlerBuilder MapMountRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetMount")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Mount)} by ID.")
            .Produces<MountRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] MountRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<MountRecord>(request.Id, null, http, store, view);
}
