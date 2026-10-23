using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record AspectRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class AspectRecordEndpoint
{
    public static RouteHandlerBuilder MapAspectRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAspect")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Aspect)} by ID.")
            .Produces<AspectRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AspectRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<AspectRecord>(request.Id, null, http, store, view);
}
