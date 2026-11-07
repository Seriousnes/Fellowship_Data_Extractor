using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.Net.Http.Headers;

using Fellowship.Codex.Services;
using Fellowship.Codex.Usage;

namespace Fellowship.Codex;

/// <summary>
/// Validators on page responses to crawlers, so a recrawl of an unchanged page is a 304 with no render.
/// A page changes only with the image or the export, so the tag hashes those two and a restart keeps it.
/// The date is the newer of the image's build and the export's build date.
/// <para>
/// A browser receives no validator. A prerendered page carries a component descriptor valid for five
/// minutes, and a browser reusing an older copy after a 304 could not start its circuit.
/// </para>
/// </summary>
public sealed class CrawlerValidators(RequestDelegate next, ExportHistoryHolder export, IConfiguration configuration)
{
    public const string ImageKey = "Codex:Image";

    private const string Revalidate = "no-cache";

    private const string NoStore = "no-store";

    private const int TagBytes = 16;

    private readonly Lazy<string> tag = new(() => Tag(configuration[ImageKey], export.History.Files));

    private readonly Lazy<DateTimeOffset> modified = new(() => Modified(
        File.GetLastWriteTimeUtc(typeof(CrawlerValidators).Assembly.Location), export.History.Snapshot.Settings.BuildDate));

    /// <summary>The tag of one image and export.</summary>
    public static string Tag(string? image, ExportFiles files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        hash.AppendData(Encoding.UTF8.GetBytes(image ?? string.Empty));
        hash.AppendData(files.Entities.Span);
        hash.AppendData(files.Settings.Span);

        return $"W/\"{Convert.ToHexStringLower(hash.GetHashAndReset().AsSpan(0, TagBytes))}\"";
    }

    private static DateTimeOffset Modified(DateTime image, DateTimeOffset? export)
    {
        var built = new DateTimeOffset(image, TimeSpan.Zero);
        var newest = export > built ? export.Value : built;

        return DateTimeOffset.FromUnixTimeSeconds(newest.ToUnixTimeSeconds());
    }

    public Task InvokeAsync(HttpContext http)
    {
        if (!Page(http)) return next(http);

        if (UserAgents.Classify(http.Request.Headers.UserAgent).Kind != ClientKind.Crawler)
        {
            http.Response.OnStarting(() =>
            {
                http.Response.Headers.CacheControl = NoStore;
                return Task.CompletedTask;
            });

            return next(http);
        }

        if (Fresh(http.Request))
        {
            http.Response.StatusCode = StatusCodes.Status304NotModified;
            Stamp(http.Response);

            return Task.CompletedTask;
        }

        http.Response.OnStarting(() =>
        {
            if (http.Response.StatusCode == StatusCodes.Status200OK) Stamp(http.Response);
            else http.Response.Headers.CacheControl = NoStore;

            return Task.CompletedTask;
        });

        return next(http);
    }

    private void Stamp(HttpResponse response)
    {
        response.Headers.ETag = tag.Value;
        response.Headers.LastModified = modified.Value.ToString("R");
        response.Headers.CacheControl = Revalidate;
    }

    // If-None-Match decides when present, and If-Modified-Since decides otherwise.
    private bool Fresh(HttpRequest request)
    {
        var headers = request.GetTypedHeaders();

        if (headers.IfNoneMatch is { Count: > 0 } matches)
        {
            return matches.Any(match => match.Equals(EntityTagHeaderValue.Any) || match.Compare(EntityTagHeaderValue.Parse(tag.Value), useStrongComparison: false));
        }

        return headers.IfModifiedSince is { } since && since >= modified.Value;
    }

    private static bool Page(HttpContext http) =>
        (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) &&
        http.GetEndpoint() is { } endpoint &&
        endpoint.Metadata.GetMetadata<ComponentTypeMetadata>() is not null &&
        endpoint.Metadata.GetMetadata<IAuthorizeData>() is null;
}
