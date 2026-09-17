using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class GemRecordEndpoint
{
    public static RouteHandlerBuilder MapGemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGem")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Gem)} by ID.")
            .Produces<GemRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<GemRecord>(request.Id, null, http, store, view);
}
