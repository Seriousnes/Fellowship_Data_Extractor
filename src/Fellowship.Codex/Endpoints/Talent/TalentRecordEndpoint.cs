using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record TalentRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TalentRecordEndpoint
{
    public static RouteHandlerBuilder MapTalentRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTalent")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Talent)}'s record.")
            .Produces<TalentRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TalentRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<TalentRecord>(request.Id, null, http, store, view);
}
