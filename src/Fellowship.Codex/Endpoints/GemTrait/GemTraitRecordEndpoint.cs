using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemTraitRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class GemTraitRecordEndpoint
{
    public static RouteHandlerBuilder MapGemTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGemTrait")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.GemTrait)} by ID.")
            .Produces<GemTraitRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemTraitRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<GemTraitRecord>(request.Id, null, http, store, view);
}
