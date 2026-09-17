using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record SetBonusRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class SetBonusRecordEndpoint
{
    public static RouteHandlerBuilder MapSetBonusRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSetBonus")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.SetBonus)} by ID.")
            .Produces<SetBonusRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] SetBonusRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<SetBonusRecord>(request.Id, null, http, store, view);
}
