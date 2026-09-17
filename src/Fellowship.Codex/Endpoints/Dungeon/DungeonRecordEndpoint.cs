using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record DungeonRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class DungeonRecordEndpoint
{
    public static RouteHandlerBuilder MapDungeonRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetDungeon")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Dungeon)} by ID.")
            .Produces<DungeonRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] DungeonRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view) =>
        Records.One<DungeonRecord>(request.Id, null, http, store, view);
}
