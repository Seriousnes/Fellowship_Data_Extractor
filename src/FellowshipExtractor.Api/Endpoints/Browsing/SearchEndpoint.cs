using System.ComponentModel;
using System.Globalization;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;
using FellowshipExtractor.Api.Services;

namespace FellowshipExtractor.Api.Endpoints;

public sealed record SearchRequest
{
    [Description("The text to match. An empty one answers no results.")]
    public string? Q { get; init; }

    [Description("One entity type slug, to narrow the results to it.")]
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
        var narrowed = StatQuery.Given(request.Type);

        if (narrowed is not null && !EntityTypes.TryParse(narrowed, out _))
        {
            return Results.Problem(
                title: "No such type.",
                detail: PublicTypes.NoSuchType(narrowed),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var limit = SuggestionService.Limit(request.Limit?.ToString(CultureInfo.InvariantCulture));

        return Results.Ok(suggestions.For(request.Q ?? string.Empty, narrowed, limit));
    }
}
