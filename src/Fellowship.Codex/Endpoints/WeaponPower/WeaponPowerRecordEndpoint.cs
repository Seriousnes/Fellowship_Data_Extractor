using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

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
            .Produces<WeaponPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] WeaponPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<WeaponPowerRecord>(request.Id, null, http, store, view);
}
