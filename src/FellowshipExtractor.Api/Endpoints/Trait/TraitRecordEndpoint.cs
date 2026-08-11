using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record TraitRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TraitRecordEndpoint
{
    public static RouteHandlerBuilder MapTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTrait")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Trait)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TraitRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Trait, request.Id, null, http, store, view);
}
