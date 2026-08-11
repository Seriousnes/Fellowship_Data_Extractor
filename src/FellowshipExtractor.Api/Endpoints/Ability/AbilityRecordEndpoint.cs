using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record AbilityRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class AbilityRecordEndpoint
{
    public static RouteHandlerBuilder MapAbilityRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAbility")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Ability)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AbilityRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Ability, request.Id, null, http, store, view);
}
