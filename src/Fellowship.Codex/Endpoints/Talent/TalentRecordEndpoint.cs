using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record TalentRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class TalentRecordEndpoint
{
    public static RouteHandlerBuilder MapTalentRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTalent")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Talent)} by ID.")
            .Produces<TalentRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TalentRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<TalentRecord>(request.Id, null, http, store, view);
}
