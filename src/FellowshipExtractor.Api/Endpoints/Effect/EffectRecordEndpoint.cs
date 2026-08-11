using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record EffectRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class EffectRecordEndpoint
{
    public static RouteHandlerBuilder MapEffectRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetEffect")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Effect)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] EffectRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Effect, request.Id, null, http, store, view);
}
