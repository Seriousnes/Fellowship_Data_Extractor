using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record TalentRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TalentRecordEndpoint
{
    public static RouteHandlerBuilder MapTalentRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTalent")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Talent)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TalentRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Talent, request.Id, null, store, view);
}
