using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record GemTraitRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class GemTraitRecordEndpoint
{
    public static RouteHandlerBuilder MapGemTraitRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetGemTrait")
            .WithSummary($"One {PublicTypes.Noun(EntityType.GemTrait)}'s record.")
            .Produces<GemTraitRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] GemTraitRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<GemTraitRecord>(request.Id, null, http, store, view);
}
