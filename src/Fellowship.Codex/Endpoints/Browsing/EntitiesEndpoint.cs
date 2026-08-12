using System.ComponentModel;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public sealed record EntitiesRequest
{
    [Description("Free text matched against every name.")]
    public string? Q { get; init; }

    [Description("Entity type slug, comma separated for several.")]
    public string? Type { get; init; }

    [Description("The column to order by. Defaults to name.")]
    public string? Sort { get; init; }

    [Description("Given as desc to order the other way.")]
    public string? Dir { get; init; }

    [Description("The facet to group the page by.")]
    public string? Group { get; init; }

    [Description("Which page to answer, from 1.")]
    public int? Page { get; init; }

    [Description("How many rows a page holds, up to 200. Defaults to 50.")]
    public int? Size { get; init; }

    public CodexQuery ToQuery(IQueryCollection raw, ICodexService codex, out string? error) =>
        CodexQuery.Parse(raw, codex, RequestProperties.Of(typeof(EntitiesRequest)), out error);
}

public static class EntitiesEndpoint
{
    public static RouteHandlerBuilder MapEntities(this IEndpointRouteBuilder group) =>
        group.MapGet("/entities", Handle)
            .WithName("GetEntities")
            .WithSummary("Every entity a filter leaves, paged.")
            .Produces<EntitiesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

    private static IResult Handle(
        [AsParameters] EntitiesRequest request,
        HttpContext http,
        ICodexService codex,
        ICodexSearchService search,
        ICodexViewService view)
    {
        var query = request.ToQuery(http.Request.Query, codex, out var error);

        if (error is not null)
        {
            return Results.Problem(
                title: "The filter could not be read.", detail: error, statusCode: StatusCodes.Status400BadRequest);
        }

        var results = search.Run(query);

        return Results.Ok(new EntitiesResponse(
            results.Total,
            results.Page,
            results.Pages,
            [.. results.Rows.Select(row => SearchResult.From(row, view))]));
    }
}
