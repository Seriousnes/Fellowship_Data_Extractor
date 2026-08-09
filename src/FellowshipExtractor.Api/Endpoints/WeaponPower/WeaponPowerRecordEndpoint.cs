using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record WeaponPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class WeaponPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapWeaponPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetWeaponPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.WeaponPower)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] WeaponPowerRecordRequest request, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.WeaponPower, request.Id, null, store, view);
}
