using System.ComponentModel;

using Fellowship.SDK.Client;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record AbilityRecordRequest
{
    [Description("The id.")]
    public int Id { get; init; }

    [Description(AbilityReading.Difficulty)]
    public int? Difficulty { get; init; }

    public RecordReading ToReading() => new() { Difficulty = Difficulty };
}

public sealed record AbilityListRequest : RecordListRequest
{
    [Description(AbilityReading.Difficulty)]
    public int? Difficulty { get; init; }

    public override RecordReading ToReading() => new() { Difficulty = Difficulty };
}

public static class AbilityReading
{
    public const string Difficulty =
        "The difficulty to scale a creature's ability to. Stating it adds difficulty to the record of an " +
        "ability a creature owns, with its damage at that difficulty.";
}

public static class AbilityRecordEndpoint
{
    public static RouteHandlerBuilder MapAbilityRecord(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}", Handle)
            .WithName("GetAbility")
            .WithSummary($"Get {PublicTypes.Noun(EntityType.Ability)} by ID.")
            .Produces<AbilityRecord>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static IResult Handle(
        [AsParameters] AbilityRecordRequest request,
        HttpContext http,
        IEntityStoreService store,
        ICodexViewService view,
        ITooltipViewService tooltips) =>
        Records.One<AbilityRecord>(request.Id, request.ToReading(), http, store, view, tooltips);
}
