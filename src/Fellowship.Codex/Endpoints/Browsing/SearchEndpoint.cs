using System.ComponentModel;
using System.Globalization;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record SearchRequest
{
    [Description("The text to match. An empty one answers no results.")]
    public string? Q { get; init; }

    [Description("One entity type slug.")]
    public string? Type { get; init; }

    [Description("How many results to answer, 1 to 100. Defaults to 10.")]
    public int? Limit { get; init; }
}

public static class SearchEndpoint
{
    public static RouteHandlerBuilder MapSearch(this IEndpointRouteBuilder group) =>
        group.MapGet("/search", Handle)
            .WithName("Search")
            .WithSummary("Ranked name matches, across every type or the one the type= slug names.")
            .Produces<SearchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static IResult Handle([AsParameters] SearchRequest request, ISuggestionService suggestions)
    {
        var type = StatQuery.NullIfEmpty(request.Type);

        if (type is not null && !EntityTypes.TryParse(type, out _))
        {
            return Results.Problem(
                title: "No such type.",
                detail: PublicTypes.NoSuchType(type),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var limit = SuggestionService.Limit(request.Limit?.ToString(CultureInfo.InvariantCulture));

        return Results.Ok(suggestions.For(request.Q ?? string.Empty, type, limit));
    }
}
