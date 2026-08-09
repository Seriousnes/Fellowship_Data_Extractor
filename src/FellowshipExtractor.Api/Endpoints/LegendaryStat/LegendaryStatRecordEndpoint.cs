using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
        [AsParameters] LegendaryStatRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.LegendaryStat, request.Id, null, store, view);
}
