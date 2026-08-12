using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record AbilityTooltipRequest
{
    [Description("The id to draw.")]
    public int Id { get; init; }

    [Description("The hero whose stat a PrimaryAttribute coefficient scales off.")]
    public string? Hero { get; init; }

    [Description("The dungeon difficulty to compute a creature ability's damage at.")]
    public int? Difficulty { get; init; }

    public TooltipQuery ToQuery(IQueryCollection raw, StatParameters stats, out string? error) =>
        StatQuery.Read(this, raw, stats, out error) with
        {
            Hero = StatQuery.Given(Hero),
            Difficulty = Difficulty,
        };
}

public static class AbilityTooltipEndpoint
{
    public static RouteHandlerBuilder MapAbilityTooltip(this IEndpointRouteBuilder group) =>
        group.MapGet("/{id:int}/tooltip", Handle)
            .WithName("GetAbilityTooltip")
            .WithSummary($"One {PublicTypes.Noun(EntityType.Ability)}'s tooltip as a self-contained HTML fragment.")
            .WithStats()
            .Produces<string>(StatusCodes.Status200OK, "text/html")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static Task<IResult> Handle(
        [AsParameters] AbilityTooltipRequest request,
        HttpContext http,
        ITooltipService tooltips,
        IEntityStoreService store)
    {
        var query = request.ToQuery(http.Request.Query, store.Stats, out var error);

        return error is null
            ? tooltips.Get(EntityType.Ability, request.Id, query)
            : Task.FromResult(StatQuery.Unreadable(error, store.Stats));
    }
}
