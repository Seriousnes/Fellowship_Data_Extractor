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

    /// <summary>The origin serving codex art, one PNG per name under <c>/ui</c>.</summary>
    public static Uri ArtOrigin { get; } = new("https://cdn.codex.fellowshipanalyzer.com/");

    /// <summary>The browsable page for one entity.</summary>
    public static string Page(EntityType type, int id, string? hero = null) =>
        WithQuery(new StringBuilder($"{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>The <c>/api</c> route answering one entity's record.</summary>
    public static string Record(EntityType type, int id, string? hero = null) =>
        WithQuery(new StringBuilder($"api/{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>
    /// The <c>/api</c> route answering one entity's tooltip as a self-contained HTML fragment. The route
    /// takes a <c>QUERY</c> carrying a <see cref="TooltipRequest"/> body, so the address carries no
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

    /// <summary>The <c>/api</c> route answering the catalogue of everything the build carries.</summary>
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
