using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record CompanionPetRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class CompanionPetRecordEndpoint
{
    public static RouteHandlerBuilder MapCompanionPetRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCompanionPet")
            .WithSummary($"One {PublicTypes.Noun(EntityType.CompanionPet)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CompanionPetRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.CompanionPet, request.Id, null, http, store, view);
}
