using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemAbilityRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class GemAbilityRecordEndpoint
{
    public static RouteHandlerBuilder MapGemAbilityRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGemAbility")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.GemAbility)} by ID.")
            .Produces<GemAbilityRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemAbilityRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<GemAbilityRecord>(request.Id, null, http, store, view);
}
