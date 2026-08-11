using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record TraitPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TraitPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapTraitPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTraitPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.TraitPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TraitPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.TraitPower, request.Id, null, http, store, view);
}
