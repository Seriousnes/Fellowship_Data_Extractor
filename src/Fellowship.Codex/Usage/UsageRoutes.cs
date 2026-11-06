using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.StaticAssets;

using Fellowship.Codex.Endpoints;

namespace Fellowship.Codex.Usage;

public enum UsageArea
{
    Page,
    Api,
    Card,
    Circuit,
    Static,
    Document,
    Other,
    Unmatched,
}

/// <summary>Area, route template and entity type slug of a counted request.</summary>
public readonly record struct UsageRoute(UsageArea Area, string Route, string Entity)
{
    public const string None = "";

    public const string Assets = "static asset";

    public const string Unmatched = "no route";
}

public static class UsageRoutes
{
    public const string Admin = "/admin";

    private const string Hub = "/_blazor";

    private const string Api = "/api";

    private const string SlugValue = "slug";

    public static bool IsCircuit(HttpContext http) =>
        http.Request.Path.StartsWithSegments(Hub, out var rest) && (!rest.HasValue || rest == "/");

    /// <summary>The usage route for a request, or null for a request excluded from the counts.</summary>
    public static UsageRoute? Group(HttpContext http)
    {
        var path = http.Request.Path;

        if (path.StartsWithSegments(Admin) ||
            path.StartsWithSegments(Microsoft.Extensions.Hosting.Extensions.HealthEndpointPath) ||
            path.StartsWithSegments(Microsoft.Extensions.Hosting.Extensions.AlivenessEndpointPath))
        {
            return null;
        }

        // The connection request alone is the circuit. Its negotiate and disconnect requests are Other.
        if (path.StartsWithSegments(Hub, out var rest))
        {
            return rest.HasValue && rest != "/"
                ? new UsageRoute(UsageArea.Other, $"{Hub}{rest}", UsageRoute.None)
                : new UsageRoute(UsageArea.Circuit, Hub, UsageRoute.None);
        }

        if (path.StartsWithSegments($"/{ApiDocument.Ui}") || path.StartsWithSegments(ApiDocument.Route))
        {
            return new UsageRoute(UsageArea.Document, $"/{ApiDocument.Ui}", UsageRoute.None);
        }

        var endpoint = http.GetEndpoint();

        if (endpoint is not RouteEndpoint route)
        {
            return new UsageRoute(UsageArea.Unmatched, UsageRoute.Unmatched, UsageRoute.None);
        }

        if (route.Metadata.GetMetadata<StaticAssetDescriptor>() is not null)
        {
            return new UsageRoute(UsageArea.Static, UsageRoute.Assets, UsageRoute.None);
        }

        var template = Template(route.RoutePattern);
        var entity = Entity(http);

        if (route.Metadata.GetMetadata<ComponentTypeMetadata>() is not null)
        {
            return new UsageRoute(UsageArea.Page, template, entity);
        }

        if (template.StartsWith($"/{Cards.Prefix}/", StringComparison.Ordinal))
        {
            return new UsageRoute(UsageArea.Card, template, entity);
        }

        return template.StartsWith(Api, StringComparison.Ordinal)
            ? new UsageRoute(UsageArea.Api, template, UsageRoute.None)
            : new UsageRoute(UsageArea.Other, template, UsageRoute.None);
    }

    private static string Template(RoutePattern pattern)
    {
        var text = pattern.RawText ?? UsageRoute.Unmatched;

        return text.StartsWith('/') ? text : $"/{text}";
    }

    // Only a slug the export names is kept, so a request for an arbitrary slug adds no row.
    private static string Entity(HttpContext http) =>
        http.Request.RouteValues.TryGetValue(SlugValue, out var value) &&
        value is string slug &&
        EntityTypes.TryParse(slug, out var type)
            ? EntityTypes.Slug(type)
            : UsageRoute.None;
}
