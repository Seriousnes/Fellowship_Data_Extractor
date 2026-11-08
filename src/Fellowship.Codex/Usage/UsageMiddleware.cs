using System.Diagnostics;

using Microsoft.Net.Http.Headers;

namespace Fellowship.Codex.Usage;

/// <summary>
/// Counts each request by area, route template, status and user-agent family. It runs ahead of the
/// output cache, so a response served from the cache is counted too. A 5xx response is also held in
/// <see cref="Ring{T}" /> with its address and cause.
/// </summary>
public sealed class UsageMiddleware(
    RequestDelegate next,
    UsageCounter counter,
    Ring<FailedRequest> failures,
    ClientTags clients,
    TimeProvider time)
{
    public const string OtherMethod = "OTHER";

    private const int ServerError = 500;

    private const int AddressLength = 512;

    // Any other method token is one row, so an invented method adds no rows.
    private static readonly string[] Methods =
        [HttpMethods.Get, HttpMethods.Head, HttpMethods.Post, HttpMethods.Query, HttpMethods.Options];

    public static string Method(string method) =>
        Methods.FirstOrDefault(known => HttpMethods.Equals(known, method)) ?? OtherMethod;

    public async Task InvokeAsync(HttpContext http)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? thrown = null;

        try
        {
            await next(http);
        }
        catch (Exception exception)
        {
            thrown = exception;
            throw;
        }
        finally
        {
            if (UsageRoutes.Group(http) is { } route)
            {
                var failed = thrown is not null;
                var status = failed && !http.Response.HasStarted ? StatusCodes.Status500InternalServerError : http.Response.StatusCode;
                var method = Method(http.Request.Method);
                var agent = UserAgents.Classify(http.Request.Headers.UserAgent);
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

                // A circuit's request lasts as long as the circuit, so its duration is no response time.
                double? milliseconds = route.Area == UsageArea.Circuit ? null : elapsed;

                counter.Request(
                    route,
                    method,
                    status,
                    http.Response.Headers.ContainsKey(HeaderNames.Age),
                    agent,
                    milliseconds);

                if (status >= ServerError)
                {
                    var reason = FailureTag.Read(http, failed);

                    counter.Failure(route, method, status, reason, agent);
                    failures.Add(new FailedRequest(
                        time.GetUtcNow(), reason, status, method, route.Area, route.Route, Address(http.Request), agent,
                        clients.Tag(http.Connection.RemoteIpAddress), elapsed, thrown?.GetType().FullName));
                }
            }
        }
    }

    private static string Address(HttpRequest request)
    {
        var address = $"{request.Path}{request.QueryString}";

        return address.Length <= AddressLength ? address : address[..AddressLength];
    }
}
