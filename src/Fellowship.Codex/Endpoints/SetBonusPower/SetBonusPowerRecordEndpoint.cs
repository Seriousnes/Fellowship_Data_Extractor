using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record SetBonusPowerRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class SetBonusPowerRecordEndpoint
{
    public static RouteHandlerBuilder MapSetBonusPowerRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSetBonusPower")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.SetBonusPower)} by ID.")
            .Produces<SetBonusPowerRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] SetBonusPowerRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<SetBonusPowerRecord>(request.Id, null, http, store, view);
}
