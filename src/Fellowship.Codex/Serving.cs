using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;

namespace Fellowship.Codex;

public static class Serving
{
    public const string Policy = "codex";

    public const string Origins = "origins";

    private const int Renders = 8;

    private const int Queued = 32;

    private const long Held = 64L * 1024 * 1024;

    private static readonly TimeSpan Kept = TimeSpan.FromDays(365);

    private static readonly TimeSpan Longest = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddServing(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            options.SizeLimit = Held;

            options.AddPolicy(Policy, policy => policy
                .SetVaryByQuery("*")
                .SetVaryByHeader("Accept-Encoding")
                .Expire(Kept));
        });

        services.AddCors(options => options.AddPolicy(Origins, policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .WithMethods(HttpMethods.Get, HttpMethods.Head, HttpMethods.Options)));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status503ServiceUnavailable;

            options.AddConcurrencyLimiter(Policy, limiter =>
            {
                limiter.PermitLimit = Renders;
                limiter.QueueLimit = Queued;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        services.AddRequestTimeouts(options => options.AddPolicy(Policy, new RequestTimeoutPolicy
        {
            Timeout = Longest,
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

    public static IEndpointConventionBuilder ServePages(this IEndpointConventionBuilder builder) =>
        new ApartFromTheCircuit(builder).Serve();

    private sealed class ApartFromTheCircuit(IEndpointConventionBuilder endpoints) : IEndpointConventionBuilder
    {
        private const string Circuit = "_blazor";

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
            route.RoutePattern.RawText?.TrimStart('/').StartsWith(Circuit, StringComparison.OrdinalIgnoreCase) is true;
    }
}
