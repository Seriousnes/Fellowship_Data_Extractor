using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record SetBonusRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class SetBonusRecordEndpoint
{
    public static RouteHandlerBuilder MapSetBonusRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetSetBonus")
            .WithSummary($"One {PublicTypes.Noun(EntityType.SetBonus)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] SetBonusRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.SetBonus, request.Id, null, http, store, view);
}
