using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record CreatureRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class CreatureRecordEndpoint
{
    public static RouteHandlerBuilder MapCreatureRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCreature")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Creature)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CreatureRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Creature, request.Id, null, store, view);
}
