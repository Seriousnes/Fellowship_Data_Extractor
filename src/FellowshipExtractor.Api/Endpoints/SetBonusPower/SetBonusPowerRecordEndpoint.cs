using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record SetBonusPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class SetBonusPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapSetBonusPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSetBonusPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.SetBonusPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] SetBonusPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.SetBonusPower, request.Id, null, http, store, view);
}
