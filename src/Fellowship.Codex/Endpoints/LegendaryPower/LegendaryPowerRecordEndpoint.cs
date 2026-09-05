using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record LegendaryPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class LegendaryPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapLegendaryPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetLegendaryPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.LegendaryPower)}'s record.")
            .Produces<LegendaryPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] LegendaryPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<LegendaryPowerRecord>(request.Id, null, http, store, view);
}
