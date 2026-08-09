using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record GemTraitRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class GemTraitRecordEndpoint
{
    public static RouteHandlerBuilder MapGemTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGemTrait")
            .WithSummary($"One {PublicTypes.Noun(EntityType.GemTrait)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemTraitRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.GemTrait, request.Id, null, store, view);
}
