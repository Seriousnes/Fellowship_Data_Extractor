using System.ComponentModel;

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
        CodexQuery.Parse(raw, codex, RequestProperties.Of(typeof(EntitiesRequest)), null, out error);
}

public static class EntitiesEndpoint
{
    public const string Name = "GetEntities";

    /// <summary>File formats of the unpaged list. Each format is also the extension of its address.</summary>
    public static readonly IReadOnlyList<string> Downloads = [EntityTable.Csv, EntityTable.Json];

    public static string DownloadName(string format) => "DownloadEntities" + char.ToUpperInvariant(format[0]) + format[1..];

    public static IEndpointRouteBuilder MapEntities(this IEndpointRouteBuilder group)
    {
        group.MapGet("/entities", Handle)
            .WithName(Name)
            .WithSummary("Every entity a filter leaves, paged.")
            .Produces<EntitiesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        foreach (var format in Downloads)
        {
            group.MapGet("/entities." + format, (
                    [AsParameters] EntitiesRequest request,
                    HttpContext http,
                    ICodexService codex,
                    ICodexSearchService search,
                    ICodexViewService view) => Download(format, request, http, codex, search, view))
                .WithName(DownloadName(format))
                .WithSummary($"Every entity a filter leaves, unpaged, in a {format.ToUpperInvariant()} file with its facets and measures.")
                .Produces(StatusCodes.Status200OK, contentType: EntityTable.ContentType(format))
                .ProducesProblem(StatusCodes.Status400BadRequest);
        }

        return group;
    }

    private static IResult Download(
        string format,
        EntitiesRequest request,
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

        var rows = search.Run(query with { Page = 1, PageSize = int.MaxValue / 2 }).Rows;
        var table = EntityTable.Of(rows, codex, view, Origins.Of(http));
        var named = query.Types is [var only] ? EntityTypes.Slug(only) : "entities";

        return Results.File(
            table.Write(format),
            EntityTable.ContentType(format),
            $"fellowship-codex-{named}.{format}");
    }

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
            [.. results.Rows.Select(row => SearchResults.From(row, view))]));
    }
}
