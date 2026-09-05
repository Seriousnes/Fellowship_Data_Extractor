using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record TraitPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TraitPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapTraitPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTraitPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.TraitPower)}'s record.")
            .Produces<TraitPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TraitPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<TraitPowerRecord>(request.Id, null, http, store, view);
}
