using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Fellowship.Codex.Tests;

public sealed class EndpointTests : IClassFixture<RouteTests.Factory>
{
    private readonly IReadOnlyList<RouteEndpoint> endpoints;

    public EndpointTests(RouteTests.Factory factory) =>
        endpoints = [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    [Fact]
    public void Every_entity_type_registers_a_record_route_and_a_tooltip_route()
    {
        var named = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();

        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            if (!named.Contains($"Get{type}")) missing.Add($"Get{type}");
            if (!named.Contains($"Get{type}Tooltip")) missing.Add($"Get{type}Tooltip");
        }

        Assert.True(missing.Count == 0, "No endpoint file names " + string.Join(", ", missing) + ".");
    }

    [Fact]
    public void Every_entity_type_is_addressed_by_a_literal_path_naming_it()
    {
        var patterns = endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            Assert.Contains($"/api/{EntityTypes.Slug(type)}/{{id:int}}", patterns);
            Assert.Contains($"/api/{EntityTypes.Slug(type)}/{{id:int}}/tooltip", patterns);
        }
    }

    [Fact]
    public void An_unlisted_type_registers_no_route_at_all()
    {
        var patterns = endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var type in Enum.GetValues<EntityType>().Where(named => !PublicTypes.IsListed(named)))
        {
            Assert.DoesNotContain($"/api/{EntityTypes.Slug(type)}/{{id:int}}", patterns);
            Assert.DoesNotContain($"/api/{EntityTypes.Slug(type)}/{{id:int}}/tooltip", patterns);
        }
    }

    [Fact]
    public void The_circuit_carries_no_output_cache_no_concurrency_limit_and_no_request_timeout()
    {
        var circuit = endpoints.Where(Circuit).ToArray();

        Assert.NotEmpty(circuit);

        foreach (var endpoint in circuit)
        {
            Assert.Null(endpoint.Metadata.GetMetadata<IOutputCachePolicy>());
            Assert.Null(endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>());
            Assert.Null(endpoint.Metadata.GetMetadata<RequestTimeoutAttribute>());
        }
    }

    [Fact]
    public void Every_rendered_page_is_cached_shed_and_timed_out()
    {
        var pages = endpoints
            .Where(endpoint => !Circuit(endpoint) && endpoint.Metadata.GetMetadata<RootComponentMetadata>() is not null)
            .ToArray();

        Assert.NotEmpty(pages);

        foreach (var page in pages)
        {
            Assert.NotNull(page.Metadata.GetMetadata<IOutputCachePolicy>());
            Assert.Equal(Serving.Policy, page.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
            Assert.Equal(Serving.Policy, page.Metadata.GetMetadata<RequestTimeoutAttribute>()?.PolicyName);
        }
    }

    private static bool Circuit(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText?.TrimStart('/').StartsWith("_blazor", StringComparison.OrdinalIgnoreCase) is true;
}
