using System.Security.Cryptography;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Fellowship.Codex;

public static class Serving
{
    public const string Policy = "codex";

    public const string Origins = "origins";

    private const int PermitLimit = 8;

    private const int QueueLimit = 32;

    private const long SizeLimit = 64L * 1024 * 1024;

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddServing(this IServiceCollection services)
    {
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

            options.AddConcurrencyLimiter(Policy, limiter =>
            {
                limiter.PermitLimit = PermitLimit;
                limiter.QueueLimit = QueueLimit;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        services.AddRequestTimeouts(options => options.AddPolicy(Policy, new RequestTimeoutPolicy
        {
            Timeout = Timeout,
            TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
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
        var pages = new ExceptBlazorHub(builder);

        pages.RequireRateLimiting(Policy);
        pages.WithRequestTimeout(Policy);

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

    private sealed class ExceptBlazorHub(IEndpointConventionBuilder endpoints) : IEndpointConventionBuilder
    {
        private const string BlazorHub = "_blazor";

        public void Add(Action<EndpointBuilder> convention) =>
            endpoints.Add(endpoint =>
            {
                if (Hub(endpoint)) return;

                convention(endpoint);
            });

        public void Finally(Action<EndpointBuilder> convention) =>
            endpoints.Finally(endpoint =>
            {
                if (Hub(endpoint)) return;

                convention(endpoint);
            });

        private static bool Hub(EndpointBuilder endpoint) =>
            endpoint is RouteEndpointBuilder route &&
            route.RoutePattern.RawText?.TrimStart('/').StartsWith(BlazorHub, StringComparison.OrdinalIgnoreCase) is true;
    }
}
