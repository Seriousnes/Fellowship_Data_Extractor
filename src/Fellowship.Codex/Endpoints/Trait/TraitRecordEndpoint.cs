using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record TraitRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class TraitRecordEndpoint
{
    public static RouteHandlerBuilder MapTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetTrait")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Trait)}'s record.")
            .Produces<TraitRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] TraitRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<TraitRecord>(request.Id, null, http, store, view);
}
