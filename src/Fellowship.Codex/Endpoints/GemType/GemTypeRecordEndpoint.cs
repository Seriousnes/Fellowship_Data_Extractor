using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemTypeRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class GemTypeRecordEndpoint
{
    public static RouteHandlerBuilder MapGemTypeRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGemType")
            .WithSummary($"One {PublicTypes.Noun(EntityType.GemType)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemTypeRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.GemType, request.Id, null, http, store, view);
}
