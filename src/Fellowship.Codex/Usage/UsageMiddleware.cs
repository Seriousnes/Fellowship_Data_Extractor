using System.Diagnostics;

using Microsoft.Net.Http.Headers;

namespace Fellowship.Codex.Usage;

/// <summary>
/// Counts each request by area, route template, status and user-agent family. It runs ahead of the
/// output cache, so a response served from the cache is counted too.
/// </summary>
public sealed class UsageMiddleware(RequestDelegate next, UsageCounter counter)
{
    public const string OtherMethod = "OTHER";

    // Any other method token is one row, so an invented method adds no rows.
    private static readonly string[] Methods =
        [HttpMethods.Get, HttpMethods.Head, HttpMethods.Post, HttpMethods.Query, HttpMethods.Options];

    public static string Method(string method) =>
        Methods.FirstOrDefault(known => HttpMethods.Equals(known, method)) ?? OtherMethod;

    public async Task InvokeAsync(HttpContext http)
    {
        var started = Stopwatch.GetTimestamp();
        var failed = false;

        try
        {
            await next(http);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            if (UsageRoutes.Group(http) is { } route)
            {
                var status = failed && !http.Response.HasStarted ? StatusCodes.Status500InternalServerError : http.Response.StatusCode;

                // A circuit's request lasts as long as the circuit, so its duration is no response time.
                double? milliseconds = route.Area == UsageArea.Circuit
                    ? null
                    : Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                counter.Request(
                    route,
                    Method(http.Request.Method),
                    status,
                    http.Response.Headers.ContainsKey(HeaderNames.Age),
                    UserAgents.Classify(http.Request.Headers.UserAgent),
                    milliseconds);
            }
        }
    }
}
