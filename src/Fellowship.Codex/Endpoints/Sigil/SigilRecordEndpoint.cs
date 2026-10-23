using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record SigilRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class SigilRecordEndpoint
{
    public static RouteHandlerBuilder MapSigilRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSigil")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Sigil)} by ID.")
            .Produces<SigilRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] SigilRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<SigilRecord>(request.Id, null, http, store, view);
}
