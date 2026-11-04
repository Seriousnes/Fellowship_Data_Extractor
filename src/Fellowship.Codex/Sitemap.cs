using System.Reflection;
using System.Xml;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Components.Pages;
using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex;

public sealed record SitemapUrl(string Path, DateTimeOffset? Modified);

/// <summary>
/// The sitemap of every page a crawler can reach without a query: each page route with no parameter,
/// each category, each entity with a page, and each build's patch notes. An entity's date is the build
/// whose patch notes last name it, and every other page's date is the newest build. A build with no
/// recorded date gives no date.
/// </summary>
public static class Sitemap
{
    public const string FileName = "sitemap.xml";

    /// <summary>File the run writes the sitemap to and exits, instead of serving.</summary>
    public const string OutputKey = "Sitemap:Output";

    /// <summary>Scheme and host the sitemap's absolute URLs start with.</summary>
    public const string OriginKey = "Sitemap:Origin";

    /// <summary>The protocol's limit on URLs in one sitemap file.</summary>
    public const int Limit = 50_000;

    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static async Task<bool> WriteIfRequestedAsync(WebApplication app)
    {
        if (app.Configuration[OutputKey] is not { Length: > 0 } output) return false;

        var origin = app.Configuration[OriginKey] is { Length: > 0 } named
            ? new Uri(named)
            : throw new InvalidOperationException($"{OutputKey} names the sitemap file, and {OriginKey} must name the site's scheme and host.");

        var urls = Urls(app.Services);

        if (urls.Count > Limit) throw new InvalidOperationException($"The sitemap has {urls.Count} URLs, past the limit of {Limit}.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, Xml(urls, origin));

        app.Logger.LogInformation("Wrote {Count} URLs to {Output}.", urls.Count, output);

        return true;
    }

    public static IReadOnlyList<SitemapUrl> Urls(IServiceProvider services)
    {
        var store = services.GetRequiredService<IEntityStoreService>();
        var ui = services.GetRequiredService<ICodexViewService>();
        var history = services.GetRequiredService<IExportHistoryService>();
        var notes = services.GetRequiredService<IPatchNotesService>();

        var newest = history.Builds is [var latest, ..] ? notes.Date(latest) : null;
        var urls = new List<SitemapUrl>();

        urls.AddRange(Routes().Select(path => new SitemapUrl(path, newest)));
        urls.AddRange(Categories.All.Select(category => new SitemapUrl($"/{category.Slug}", newest)));
        urls.AddRange(notes.Builds.Select(build => new SitemapUrl($"{PatchNotes.Href}/{build}", notes.Date(build))));
        urls.AddRange(store.Entities
            .Where(PublicTypes.IsSearched)
            .Select(document => new SitemapUrl(ui.Path(document), notes.Date(Changed(notes, history.Builds, document)))));

        return [.. urls.DistinctBy(url => url.Path, StringComparer.Ordinal)];
    }

    // The newest build whose patch notes name the entity, or the oldest build when none does.
    private static string Changed(IPatchNotesService notes, IReadOnlyList<string> builds, EntityDocument document)
    {
        var key = new DocumentKey(EntityTypes.Slug(EntityTypes.TypeOf(document)), document.Id);

        return notes.Builds.FirstOrDefault(build => notes.Of(build)?.Has(key) is true) ?? builds[^1];
    }

    // Each page route with no parameter, except the not-found page and pages behind sign-in.
    private static IEnumerable<string> Routes() =>
        typeof(Sitemap).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type) &&
                           type != typeof(NotFound) &&
                           type.GetCustomAttribute<AuthorizeAttribute>() is null)
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
            .Select(route => route.Template)
            .Where(template => !template.Contains('{'))
            .Order(StringComparer.Ordinal);

    public static string Xml(IEnumerable<SitemapUrl> urls, Uri origin)
    {
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var bytes = new MemoryStream();

        using (var xml = XmlWriter.Create(bytes, new XmlWriterSettings { Indent = true, Encoding = utf8 }))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("urlset", Namespace);

            foreach (var url in urls)
            {
                xml.WriteStartElement("url", Namespace);
                xml.WriteElementString("loc", Namespace, new Uri(origin, url.Path).AbsoluteUri);

                if (url.Modified is { } modified)
                {
                    xml.WriteElementString("lastmod", Namespace, modified.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        return utf8.GetString(bytes.ToArray());
    }
}
