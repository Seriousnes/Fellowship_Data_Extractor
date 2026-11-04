using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.Net.Http.Headers;

using Fellowship.Codex.Usage;

namespace Fellowship.Codex;

/// <summary>
/// Validators on page responses to crawlers, so a recrawl of an unchanged page is a 304 with no render.
/// The export loads once at startup and the code is fixed per image, so a page changes only across a
/// restart, and the startup time validates every page.
/// <para>
/// A browser receives no validator. A prerendered page carries a component descriptor valid for five
/// minutes, and a browser reusing an older copy after a 304 could not start its circuit.
/// </para>
/// </summary>
public sealed class CrawlerValidators(RequestDelegate next, TimeProvider time)
{
    private const string Revalidate = "no-cache";

    private const string NoStore = "no-store";

    private readonly DateTimeOffset started = Truncated(time.GetUtcNow());

    private string Tag => $"W/\"{started.ToUnixTimeSeconds():x}\"";

    private static DateTimeOffset Truncated(DateTimeOffset at) => DateTimeOffset.FromUnixTimeSeconds(at.ToUnixTimeSeconds());

    public Task InvokeAsync(HttpContext http)
    {
        if (!Page(http)) return next(http);

        if (UserAgents.Classify(http.Request.Headers.UserAgent).Kind != ClientKind.Crawler)
        {
            http.Response.OnStarting(() =>
            {
                http.Response.Headers.CacheControl = NoStore;
                return Task.CompletedTask;
            });

            return next(http);
        }

        var tag = Tag;

        if (Fresh(http.Request, tag))
        {
            http.Response.StatusCode = StatusCodes.Status304NotModified;
            Stamp(http.Response, tag);

            return Task.CompletedTask;
        }

        http.Response.OnStarting(() =>
        {
            if (http.Response.StatusCode == StatusCodes.Status200OK) Stamp(http.Response, tag);
            else http.Response.Headers.CacheControl = NoStore;

            return Task.CompletedTask;
        });

        return next(http);
    }

    private void Stamp(HttpResponse response, string tag)
    {
        response.Headers.ETag = tag;
        response.Headers.LastModified = started.ToString("R");
        response.Headers.CacheControl = Revalidate;
    }

    // If-None-Match decides when present, and If-Modified-Since decides otherwise.
    private bool Fresh(HttpRequest request, string tag)
    {
        var headers = request.GetTypedHeaders();

        if (headers.IfNoneMatch is { Count: > 0 } matches)
        {
            return matches.Any(match => match.Equals(EntityTagHeaderValue.Any) || match.Compare(EntityTagHeaderValue.Parse(tag), useStrongComparison: false));
        }

        return headers.IfModifiedSince is { } since && since >= started;
    }

    private static bool Page(HttpContext http) =>
        (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) &&
        http.GetEndpoint() is { } endpoint &&
        endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() is not null &&
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is null;
}
