using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record LegendaryPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class LegendaryPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapLegendaryPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetLegendaryPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.LegendaryPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] LegendaryPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.LegendaryPower, request.Id, null, http, store, view);
}
