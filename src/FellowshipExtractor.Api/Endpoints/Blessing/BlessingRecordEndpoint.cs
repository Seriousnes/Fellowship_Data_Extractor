using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record BlessingRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class BlessingRecordEndpoint
{
    public static RouteHandlerBuilder MapBlessingRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetBlessing")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Blessing)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] BlessingRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Blessing, request.Id, null, http, store, view);
}
