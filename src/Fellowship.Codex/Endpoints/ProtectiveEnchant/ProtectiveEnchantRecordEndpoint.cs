using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record ProtectiveEnchantRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }
}

public static class ProtectiveEnchantRecordEndpoint
{
    public static RouteHandlerBuilder MapProtectiveEnchantRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetProtectiveEnchant")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.ProtectiveEnchant)} by ID.")
            .Produces<ProtectiveEnchantRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] ProtectiveEnchantRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<ProtectiveEnchantRecord>(request.Id, null, http, store, view);
}
