using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ItemRecordRequest
{
    [Description("The Item id.")]
    public int Id { get; init; }

    [Description("Hero variant of the Item.")]
    public string? Hero { get; init; }
}

public static class ItemRecordEndpoint
{
    public static RouteHandlerBuilder MapItemRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetItem")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Item)} by ID.")
            .Produces<ItemRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ItemRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<ItemRecord>(request.Id, request.Hero, http, store, view);
}
