using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record DungeonAffixRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class DungeonAffixRecordEndpoint
{
    public static RouteHandlerBuilder MapDungeonAffixRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetDungeonAffix")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.DungeonAffix)} by ID.")
            .Produces<DungeonAffixRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] DungeonAffixRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<DungeonAffixRecord>(request.Id, null, http, store, view);
}
