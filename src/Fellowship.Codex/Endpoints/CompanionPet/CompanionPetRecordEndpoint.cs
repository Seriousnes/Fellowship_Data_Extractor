using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record CompanionPetRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class CompanionPetRecordEndpoint
{
    public static RouteHandlerBuilder MapCompanionPetRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCompanionPet")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.CompanionPet)} by ID.")
            .Produces<CompanionPetRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CompanionPetRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<CompanionPetRecord>(request.Id, null, http, store, view);
}
