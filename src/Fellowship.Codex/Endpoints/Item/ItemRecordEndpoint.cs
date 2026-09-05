using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ItemRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }

    [Description("The hero whose reading of the id to answer, where one id reads differently per hero.")]
    public string? Hero { get; init; }
}

public static class ItemRecordEndpoint
{
    public static RouteHandlerBuilder MapItemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetItem")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Item)}'s record.")
            .Produces<ItemRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ItemRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<ItemRecord>(request.Id, request.Hero, http, store, view);
}
