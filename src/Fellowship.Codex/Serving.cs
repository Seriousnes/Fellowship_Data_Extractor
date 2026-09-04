using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;

namespace Fellowship.Codex;

public static class Serving
{
    public const string Policy = "codex";

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
}
