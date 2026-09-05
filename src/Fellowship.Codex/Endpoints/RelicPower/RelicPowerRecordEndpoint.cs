using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record RelicPowerRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class RelicPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapRelicPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetRelicPower")
            .WithSummary($"One {PublicTypes.Noun(EntityType.RelicPower)}'s record.")
            .Produces<RelicPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] RelicPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<RelicPowerRecord>(request.Id, null, http, store, view);
}
