using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record MountPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class MountPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapMountPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetMountPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.MountPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] MountPowerRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.MountPower, request.Id, null, store, view);
}
