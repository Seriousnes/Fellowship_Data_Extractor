using System.ComponentModel;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record MountRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class MountRecordEndpoint
{
    public static RouteHandlerBuilder MapMountRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetMount")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Mount)}'s record.")
            .Produces<PublicEntity>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] MountRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One(EntityType.Mount, request.Id, null, http, store, view);
}
