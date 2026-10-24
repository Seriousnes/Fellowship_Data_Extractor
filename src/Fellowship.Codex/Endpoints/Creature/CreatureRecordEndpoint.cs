using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record CreatureRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }

    [Description(CreatureReading.Difficulty)]
    public int? Difficulty { get; init; }

    public RecordReading ToReading() => new() { Difficulty = Difficulty };
}

public sealed record CreatureListRequest : RecordListRequest
{
    [Description(CreatureReading.Difficulty)]
    public int? Difficulty { get; init; }

    public override RecordReading ToReading() => new() { Difficulty = Difficulty };
}

public static class CreatureReading
{
    public const string Difficulty =
        "The difficulty to scale the creature to. Stating it adds difficulty to the record, with its health " +
        "at that difficulty.";
}

public static class CreatureRecordEndpoint
{
    public static RouteHandlerBuilder MapCreatureRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetCreature")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Creature)} by ID.")
            .Produces<CreatureRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] CreatureRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<CreatureRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}