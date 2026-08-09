using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record TalentSlotRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }

    [Description("The hero whose reading of the id to answer, where one id reads differently per hero.")]
    public string? Hero { get; init; }
}

public static class TalentSlotRecordEndpoint
{
    public static RouteHandlerBuilder MapTalentSlotRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTalentSlot")
            .WithSummary($"One {PublicTypes.Noun(EntityType.TalentSlot)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TalentSlotRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.TalentSlot, request.Id, request.Hero, store, view);
}
