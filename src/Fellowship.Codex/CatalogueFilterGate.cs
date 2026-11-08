using System.Net;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.DataProtection;

using Fellowship.Codex.Components.Pages;
using Fellowship.Codex.Models;

namespace Fellowship.Codex;

/// <summary>
/// Answers a filtered catalogue request from a client that has not passed the gate with a fixed page, so
/// the request takes no render and no rate limiter permit. Each filter link adds one filter, so the
/// links form a space of URLs with no practical end, and the crawlers of that space rotate their
/// addresses and send a browser's full headers.
/// <para>
/// Enhanced navigation names itself in its Accept header, so a click on a filter passes. A direct
/// request receives the fixed page.
/// </para>
/// <para>
/// With Turnstile configured, the fixed page is a Turnstile challenge. The page posts its token to
/// <see cref="Pass"/>. <see cref="Pass"/> checks the token with siteverify and sets a protected session
/// cookie. The page then requests the URL again. Without Turnstile, the fixed page's script sets the
/// session cookie.
/// </para>
/// <para>
/// An entity page with a filtered list, such as a dungeon's loot, has the same links, so a request for one
/// that carries a filter is gated too. A reading of the entity, such as <c>?hero=</c>, is not.
/// </para>
/// </summary>
public sealed class CatalogueFilterGate(RequestDelegate next, TurnstileSettings turnstile, IDataProtectionProvider protection)
{
    public const string Cookie = "codex-scripts";

    public const string EnhancedNavigation = "blazor-enhanced-nav=on";

    public const string Pass = "/catalogue-filter-gate";

    /// <summary>The Turnstile action of the challenge, checked against the siteverify result.</summary>
    public const string Action = "catalogue_filter";

    public static readonly TimeSpan Session = TimeSpan.FromDays(1);

    private const string Purpose = "Fellowship.Codex.CatalogueFilterGate";

    private const string Passed = "passed";

    private const string NoStore = "no-store";

    private const string ScriptDocument =
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

    private readonly string document = turnstile.SiteKey is { } siteKey ? ChallengeDocument(siteKey) : ScriptDocument;

    public Task InvokeAsync(HttpContext http)
    {
        if (!Gated(http)) return next(http);

        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers.CacheControl = NoStore;

        return http.Response.WriteAsync(document, http.RequestAborted);
    }

    /// <summary>Maps the endpoint that exchanges a Turnstile token for the session cookie.</summary>
    public static void MapPass(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(Pass, PassAsync).DisableAntiforgery().ExcludeFromDescription();

    private static async Task<IResult> PassAsync(
        HttpContext http,
        TurnstileSettings turnstile,
        TurnstileVerification verification,
        IDataProtectionProvider protection)
    {
        if (!turnstile.Challenge || !http.Request.HasFormContentType) return Results.StatusCode(StatusCodes.Status403Forbidden);

        var form = await http.Request.ReadFormAsync(http.RequestAborted);
        var token = form[TurnstileVerification.ResponseField].ToString();

        if (!await verification.PassesAsync(token, Action, http.Connection.RemoteIpAddress, http.RequestAborted))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        http.Response.Headers.CacheControl = NoStore;
        http.Response.Cookies.Append(Cookie, Protector(protection).Protect(Passed, Session), new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = Session,
        });

        return Results.NoContent();
    }

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider protection) =>
        protection.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    private bool Gated(HttpContext http) =>
        HttpMethods.IsGet(http.Request.Method) &&
        http.Request.QueryString.HasValue &&
        http.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type is { } page &&
        (page == typeof(Catalogue) || (page == typeof(EntityPage) && http.Request.Query.Keys.Any(Browses))) &&
        !HasSession(http) &&
        !http.Request.Headers.Accept.Any(accept => accept?.Contains(EnhancedNavigation, StringComparison.Ordinal) is true);

    private bool HasSession(HttpContext http)
    {
        if (!http.Request.Cookies.TryGetValue(Cookie, out var value)) return false;

        if (!turnstile.Challenge) return true;

        try
        {
            return Protector(protection).Unprotect(value, out _) == Passed;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static string ChallengeDocument(string siteKey) =>
        $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>Fellowship Codex</title>
        <script src="https://challenges.cloudflare.com/turnstile/v0/api.js" async defer></script>
        <script>
        function codexPass(token) {
          fetch('{{Pass}}', {
            method: 'POST',
            credentials: 'same-origin',
            body: new URLSearchParams({ '{{TurnstileVerification.ResponseField}}': token })
          }).then(response => {
            if (response.ok) location.replace(location.href);
            else turnstile.reset();
          }, () => turnstile.reset());
        }
        </script>
        </head>
        <body>
        <div class="cf-turnstile" data-sitekey="{{WebUtility.HtmlEncode(siteKey)}}" data-action="{{Action}}" data-callback="codexPass"></div>
        </body>
        </html>
        """;

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
