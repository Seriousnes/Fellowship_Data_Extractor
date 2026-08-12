using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class GemRecordEndpoint
{
    public static RouteHandlerBuilder MapGemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGem")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Gem)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Gem, request.Id, null, http, store, view);
}
