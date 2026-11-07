using System.Security.Cryptography;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

using Fellowship.Codex.Components.Pages;
using Fellowship.Codex.Usage;

namespace Fellowship.Codex;

public static class Serving
{
    public const string Policy = "codex";

    public const string Origins = "origins";

    public const string PatchNotesPolicy = "patch-notes";

    private const long SizeLimit = 64L * 1024 * 1024;

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddServing(this IServiceCollection services)
    {
        var load = new RequestLoad();

        services.AddSingleton(load);

        services.AddOutputCache(options =>
        {
            options.SizeLimit = SizeLimit;

            options.AddPolicy(Policy, policy => policy
                .AddPolicy<VaryByBody>()
                .SetVaryByQuery("*")
                .SetVaryByHeader("Accept-Encoding")
                .Expire(Lifetime));
        });

        services.AddCors(options => options.AddPolicy(Origins, policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .WithMethods(HttpMethods.Get, HttpMethods.Head, HttpMethods.Options, HttpMethods.Query)));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;

            options.OnRejected = (context, _) =>
            {
                FailureTag.Mark(context.HttpContext, FailureReason.Rejected);

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(Policy, _ => RateLimitPartition.Get(Policy, _ => load.Open()));

            // One build's page is 9 MB of HTML, and eight at once exhaust the heap.
            options.AddConcurrencyLimiter(PatchNotesPolicy, limiter =>
            {
                limiter.PermitLimit = 1;
                limiter.QueueLimit = RequestLoad.QueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        services.AddRequestTimeouts(options => options.AddPolicy(Policy, new RequestTimeoutPolicy
        {
            Timeout = Timeout,
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
            WriteTimeoutResponse = http =>
            {
                FailureTag.Mark(http, FailureReason.TimedOut);

                return Task.CompletedTask;
            },
        }));

        return services;
    }

    public static TBuilder Serve<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.CacheOutput(Policy);
        builder.RequireRateLimiting(Policy);
        builder.WithRequestTimeout(Policy);

        return builder;
    }

    public static IEndpointConventionBuilder ServePages(this IEndpointConventionBuilder builder)
    {
        var pages = new PublicPages(builder);

        pages.RequireRateLimiting(Policy);
        pages.WithRequestTimeout(Policy);

        builder.Add(endpoint =>
        {
            if (endpoint is RouteEndpointBuilder route &&
                route.RoutePattern.RawText?.StartsWith(PatchNotes.Href, StringComparison.OrdinalIgnoreCase) is true)
            {
                endpoint.Metadata.Add(new EnableRateLimitingAttribute(PatchNotesPolicy));
            }
        });

        return pages;
    }

    private sealed class VaryByBody : IOutputCachePolicy
    {
        private const string Body = "body";

        public async ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            var request = context.HttpContext.Request;

            if (!HttpMethods.IsQuery(request.Method)) return;

            request.EnableBuffering();

            var hash = await SHA256.HashDataAsync(request.Body, cancellation);

            request.Body.Position = 0;

            context.CacheVaryByRules.VaryByValues[Body] = Convert.ToHexString(hash);

            context.EnableOutputCaching = true;
            context.AllowCacheLookup = true;
            context.AllowCacheStorage = true;
            context.AllowLocking = true;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
            ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) =>
            ValueTask.CompletedTask;
    }

    // The dashboard takes no limiter permit, because the owner opens it while the limiter rejects.
    private sealed class PublicPages(IEndpointConventionBuilder endpoints) : IEndpointConventionBuilder
    {
        private const string BlazorHub = "_blazor";

        private static readonly string Admin = UsageRoutes.Admin.TrimStart('/');

        public void Add(Action<EndpointBuilder> convention) =>
            endpoints.Add(endpoint =>
            {
                if (Exempt(endpoint)) return;

                convention(endpoint);
            });

        public void Finally(Action<EndpointBuilder> convention) =>
            endpoints.Finally(endpoint =>
            {
                if (Exempt(endpoint)) return;

                convention(endpoint);
            });

        private static bool Exempt(EndpointBuilder endpoint) =>
            endpoint is RouteEndpointBuilder route &&
            route.RoutePattern.RawText?.TrimStart('/') is { } path &&
            (path.StartsWith(BlazorHub, StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith(Admin, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The limiter every served request shares. The rate limiter middleware disposes an idle limiter and
/// asks for a new one, so this holds the latest.
/// </summary>
public sealed class RequestLoad
{
    public const int PermitLimit = 8;

    public const int QueueLimit = 32;

    private ConcurrencyLimiter? limiter;

    public RateLimiter Open() => limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
        PermitLimit = PermitLimit,
        QueueLimit = QueueLimit,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    });

    /// <summary>Requests running and queued now. A disposed limiter was idle, so it has neither.</summary>
    public (int Running, int Queued) Now()
    {
        try
        {
            return limiter?.GetStatistics() is { } statistics
                ? ((int)(PermitLimit - statistics.CurrentAvailablePermits), (int)statistics.CurrentQueuedCount)
                : (0, 0);
        }
        catch (ObjectDisposedException)
        {
            return (0, 0);
        }
    }
}
