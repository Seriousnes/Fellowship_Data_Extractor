using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Fellowship.Codex.Tests;

public sealed class EndpointTests : IClassFixture<RouteTests.Factory>
{
    private readonly RouteTests.Factory factory;

    private readonly IReadOnlyList<RouteEndpoint> endpoints;

    public EndpointTests(RouteTests.Factory factory)
    {
        this.factory = factory;
        endpoints = [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
    }

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
            if (!named.Contains($"Query{type}Tooltip")) missing.Add($"Query{type}Tooltip");
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
    public void Every_tooltip_route_answers_query_and_nothing_else()
    {
        foreach (var type in Enum.GetValues<EntityType>().Where(PublicTypes.IsListed))
        {
            var pattern = $"/api/{EntityTypes.Slug(type)}/{{id:int}}/tooltip";

            var matching = endpoints
                .Where(candidate =>
                    string.Equals(candidate.RoutePattern.RawText, pattern, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var endpoint = Assert.Single<RouteEndpoint>(matching);

            var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();

            Assert.NotNull(methods);
            Assert.Equal(new[] { HttpMethods.Query }, methods.HttpMethods);
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
    public void Every_rendered_page_is_shed_and_timed_out_and_never_cached()
    {
        var pages = endpoints
            .Where(endpoint => !Circuit(endpoint) && endpoint.Metadata.GetMetadata<RootComponentMetadata>() is not null)
            .ToArray();

        Assert.NotEmpty(pages);

        foreach (var page in pages)
        {
            Assert.Null(page.Metadata.GetMetadata<IOutputCachePolicy>());
            Assert.Equal(Serving.Policy, page.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
            Assert.Equal(Serving.Policy, page.Metadata.GetMetadata<RequestTimeoutAttribute>()?.PolicyName);
        }
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/items")]
    public async Task Every_render_of_a_page_mints_its_own_component_descriptor(string path)
    {
        using var client = factory.CreateClient();

        await client.GetStringAsync(path, Xunit.TestContext.Current.CancellationToken);

        var carried = Descriptor(await client.GetStringAsync(path, Xunit.TestContext.Current.CancellationToken));
        var again = Descriptor(await client.GetStringAsync(path, Xunit.TestContext.Current.CancellationToken));

        Assert.NotNull(carried);
        Assert.NotEqual(carried, again);
    }

    private static string? Descriptor(string page) =>
        Regex.Match(page, "\"descriptor\":\"(?<protected>[^\"]+)\"") is { Success: true } marker
            ? marker.Groups["protected"].Value
            : null;

    private static bool Circuit(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText?.TrimStart('/').StartsWith("_blazor", StringComparison.OrdinalIgnoreCase) is true;
}
