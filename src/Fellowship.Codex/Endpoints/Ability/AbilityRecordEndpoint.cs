using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record AbilityRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class AbilityRecordEndpoint
{
    public static RouteHandlerBuilder MapAbilityRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAbility")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Ability)}'s record.")
            .Produces<AbilityRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AbilityRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<AbilityRecord>(request.Id, null, http, store, view);
}
