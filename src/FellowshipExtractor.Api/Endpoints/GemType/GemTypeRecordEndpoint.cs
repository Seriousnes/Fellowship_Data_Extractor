using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
