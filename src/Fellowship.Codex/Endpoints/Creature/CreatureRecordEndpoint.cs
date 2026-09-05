using System.ComponentModel;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Records;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record CreatureRecordRequest
{
    [Description("The id to answer.")]
    public int Id { get; init; }
}

public static class CreatureRecordEndpoint
{
    public static RouteHandlerBuilder MapCreatureRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCreature")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Creature)}'s record.")
            .Produces<CreatureRecord>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CreatureRecordRequest request, HttpContext http, IEntityStoreService store, ICodexViewService view) =>
        Records.One<CreatureRecord>(request.Id, null, http, store, view);
}
