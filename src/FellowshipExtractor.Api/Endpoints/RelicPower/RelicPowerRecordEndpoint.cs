using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record RelicPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class RelicPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapRelicPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetRelicPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.RelicPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] RelicPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.RelicPower, request.Id, null, http, store, view);
}
