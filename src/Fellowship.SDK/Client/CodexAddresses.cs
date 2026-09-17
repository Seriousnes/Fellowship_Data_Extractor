using System.Globalization;
using System.Text;

namespace Fellowship.SDK.Client;

/// <summary>
/// Writes every address the codex answers, relative to <see cref="Origin"/>. <see cref="CodexClient"/>
/// sends them over an <see cref="HttpClient"/>; a caller that fetches its own way — a browser reading a
/// tooltip fragment, say — writes the address here and sends it itself.
/// </summary>
public static class CodexAddresses
{
    /// <summary>The codex origin, serving both the browsable pages and the <c>/api</c> routes.</summary>
    public static Uri Origin { get; } = new("https://codex.fellowshipanalyzer.com/");

    /// <summary>The origin serving codex textures, one WebP per name and size under <c>/ui</c>.</summary>
    public static Uri Cdn { get; } = new("https://cdn.codex.fellowshipanalyzer.com/");

    /// <summary>
    /// The <see cref="Cdn"/> address of one image at <paramref name="size"/>. A document's texture field
    /// carries the name alone, so pass that name here. <see cref="TextureSize.Full"/> is written for every name
    /// at its native resolution; a square size is written only where the native image is at least that many
    /// pixels on its shorter edge, so fall back to <see cref="TextureSize.Full"/> when a size answers 404.
    /// </summary>
    public static Uri Texture(string name, TextureSize size) => new(Cdn, TextureSizes.Key(name, size));

    /// <summary>The browsable page for one entity.</summary>
    public static string Page(EntityType type, int id, string? hero = null) =>
        WithQuery(new StringBuilder($"{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>The <c>/api</c> route answering one entity's record.</summary>
    public static string Record(EntityType type, int id, string? hero = null) =>
        WithQuery(new StringBuilder($"api/{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>
    /// The <c>/api</c> route answering one entity's self-contained HTML tooltip fragment. The route takes
    /// a <c>QUERY</c> whose body is that type's own tooltip request record, so the address has no
    /// parameters of its own.
    /// </summary>
    public static string Tooltip(EntityType type, int id) =>
        $"api/{EntityTypes.Slug(type)}/{Number(id)}/tooltip";

    /// <summary>The <c>/api</c> route answering entities matching <paramref name="query"/> by name.</summary>
    public static string Search(string query, EntityType? type = null, int? limit = null) =>
        WithQuery(
            new StringBuilder("api/search"),
            [
                ("q", query),
                ("type", type is { } value ? EntityTypes.Slug(value) : null),
                ("limit", Number(limit)),
            ]);

    /// <summary>The <c>/api</c> route answering a filtered page of entities.</summary>
    public static string Entities(EntityFilter? filter = null)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("q", filter?.Q),
            ("type", filter?.Type is { Count: > 0 } types
                ? string.Join(",", types.Select(EntityTypes.Slug))
                : null),
            ("sort", filter?.Sort),
            ("dir", filter?.Dir),
            ("group", filter?.Group),
            ("page", Number(filter?.Page)),
            ("size", Number(filter?.Size)),
        };

        if (filter?.Facets is { Count: > 0 } facets)
        {
            parameters.AddRange(facets.Select(entry => (entry.Key, (string?)entry.Value)));
        }

        return WithQuery(new StringBuilder("api/entities"), parameters);
    }

    /// <summary>The <c>/api</c> route answering the catalogue of everything in the build.</summary>
    public const string Catalog = "api/catalog";

    private static string WithQuery(
        StringBuilder address, IReadOnlyList<(string Key, string? Value)> parameters)
    {
        var first = true;

        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrEmpty(value)) continue;

            address.Append(first ? '?' : '&');
            address.Append(Uri.EscapeDataString(key));
            address.Append('=');
            address.Append(Uri.EscapeDataString(value));

            first = false;
        }

        return address.ToString();
    }

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
