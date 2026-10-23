using System.ComponentModel;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record EnchantRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }

    [Description("Hero whose primary stat picks the version of the Enchant.")]
    public string? Hero { get; init; }
}

public static class EnchantRecordEndpoint
{
    public static RouteHandlerBuilder MapEnchantRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetEnchant")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Enchant)} by ID.")
            .Produces<EnchantRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] EnchantRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<EnchantRecord>(request.Id, request.Hero, http, store, view);
}
