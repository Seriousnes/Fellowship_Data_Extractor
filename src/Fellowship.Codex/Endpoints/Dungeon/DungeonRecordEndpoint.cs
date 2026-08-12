using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record DungeonRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class DungeonRecordEndpoint
{
    public static RouteHandlerBuilder MapDungeonRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetDungeon")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Dungeon)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] DungeonRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view) =>
        Records.One(EntityType.Dungeon, request.Id, null, http, store, view);
}
