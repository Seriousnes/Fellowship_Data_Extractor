using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
        [AsParameters] GemRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Gem, request.Id, null, store, view);
}
