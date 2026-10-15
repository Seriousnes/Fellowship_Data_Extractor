using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record WeaponPowerRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class WeaponPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapWeaponPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetWeaponPower")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.WeaponPower)} by ID.")
            .Produces<WeaponPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] WeaponPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<WeaponPowerRecord>(request.Id, null, http, store, view);
}
