using Microsoft.AspNetCore.Components.Endpoints;

using Fellowship.Codex.Components.Pages;
using Fellowship.Codex.Models;

namespace Fellowship.Codex;

/// <summary>
/// Answers a filtered catalogue request from a client that has not run a script with a fixed page, so
/// the request takes no render and no rate limiter permit. Each filter link adds one filter, so the
/// links form a space of URLs with no practical end, and the crawlers of that space rotate their
/// addresses and send a browser's full headers. Running a script is what they do not do.
/// <para>
/// Enhanced navigation names itself in its Accept header, so a click on a filter passes. A direct
/// request receives the fixed page, whose script sets the session cookie and requests the URL again.
/// </para>
/// <para>
/// An entity page with a filtered list, such as a dungeon's loot, has the same links, so a request for one
/// that carries a filter is gated too. A reading of the entity, such as <c>?hero=</c>, is not.
/// </para>
/// </summary>
public sealed class CatalogueFilterGate(RequestDelegate next)
{
    public const string Cookie = "codex-scripts";

    public const string EnhancedNavigation = "blazor-enhanced-nav=on";

    private const string NoStore = "no-store";

    private const string Document =
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Fellowship Codex</title>
        <script>
        document.cookie = '{{Cookie}}=1; path=/; samesite=lax';
        if (document.cookie.includes('{{Cookie}}=1')) location.replace(location.href);
        </script>
        </head>
        <body>
        <p>Filtered lists need scripts and cookies.</p>
        </body>
        </html>
        """;

    public Task InvokeAsync(HttpContext http)
    {
        if (!Gated(http)) return next(http);

        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers.CacheControl = NoStore;

        return http.Response.WriteAsync(Document, http.RequestAborted);
    }

    private static bool Gated(HttpContext http) =>
        HttpMethods.IsGet(http.Request.Method) &&
        http.Request.QueryString.HasValue &&
        http.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type is { } page &&
        (page == typeof(Catalogue) || (page == typeof(EntityPage) && http.Request.Query.Keys.Any(Browses))) &&
        !http.Request.Cookies.ContainsKey(Cookie) &&
        !http.Request.Headers.Accept.Any(accept => accept?.Contains(EnhancedNavigation, StringComparison.Ordinal) is true);

    private static bool Browses(string key) =>
        !QueryKeys.TryParse<ReadingParameter>(key, out _) &&
        (key == Components.Views.Dungeon.TabKey ||
         QueryKeys.TryParse<FacetType>(key, out _) ||
         QueryKeys.TryParse<FlagType>(key, out _) ||
         QueryKeys.TryParse<BrowseParameter>(key, out _) ||
         QueryKeys.TryParse<MeasureType>(Bound(key), out _));

    private static string Bound(string key) =>
        key.EndsWith("Min", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Max", StringComparison.OrdinalIgnoreCase)
            ? key[..^3]
            : key;
}
