using Microsoft.AspNetCore.Routing;

namespace FellowshipExtractor.Api.Tests;

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

        foreach (var type in Enum.GetValues<EntityType>())
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

        foreach (var type in Enum.GetValues<EntityType>())
        {
            var slug = EntityTypes.Slug(type);

            Assert.Contains($"/api/{slug}/{{id:int}}", patterns);
            Assert.Contains($"/api/{slug}/{{id:int}}/tooltip", patterns);
        }
    }
}
