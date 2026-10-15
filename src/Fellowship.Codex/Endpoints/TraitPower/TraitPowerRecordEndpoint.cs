using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record TraitPowerRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class TraitPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapTraitPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTraitPower")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.TraitPower)} by ID.")
            .Produces<TraitPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TraitPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<TraitPowerRecord>(request.Id, null, http, store, view);
}
