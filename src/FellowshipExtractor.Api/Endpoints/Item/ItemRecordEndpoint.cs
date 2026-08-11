using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

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
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ItemRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Item, request.Id, request.Hero, http, store, view);
}
