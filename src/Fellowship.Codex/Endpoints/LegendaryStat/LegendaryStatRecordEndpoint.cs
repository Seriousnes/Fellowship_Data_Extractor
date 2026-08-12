using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record LegendaryStatRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class LegendaryStatRecordEndpoint
{
    public static RouteHandlerBuilder MapLegendaryStatRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetLegendaryStat")
            .WithSummary($"One {PublicTypes.Noun(EntityType.LegendaryStat)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] LegendaryStatRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.LegendaryStat, request.Id, null, http, store, view);
}
