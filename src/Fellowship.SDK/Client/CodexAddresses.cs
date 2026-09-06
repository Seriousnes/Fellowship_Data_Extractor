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
        Written(new StringBuilder($"{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>The <c>/api</c> route answering one entity's record.</summary>
    public static string Record(EntityType type, int id, string? hero = null) =>
        Written(new StringBuilder($"api/{EntityTypes.Slug(type)}/{Number(id)}"), [("hero", hero)]);

    /// <summary>The <c>/api</c> route answering one entity's tooltip as a self-contained HTML fragment.</summary>
    public static string Tooltip(EntityType type, int id, TooltipRequest? asked = null)
    {
        var address = new StringBuilder($"api/{EntityTypes.Slug(type)}/{Number(id)}/tooltip");

        if (asked is null) return address.ToString();

        var given = new List<(string Key, string? Value)>
        {
            ("showIcon", Flag(asked.ShowIcon)),
            ("mobile", Flag(asked.Mobile)),
            ("hero", asked.Hero),
            ("difficulty", Number(asked.Difficulty)),
            ("state", asked.State),
            ("equippedTier", Number(asked.EquippedTier)),
            ("rarity", asked.Rarity),
            ("itemLevel", Number(asked.ItemLevel)),
            ("upgrades", Number(asked.Upgrades)),
            ("maxUpgrades", Number(asked.MaxUpgrades)),
            ("noSocket", Flag(asked.NoSocket)),
            ("gem", Number(asked.Gem)),
            ("potentialPower", Number(asked.PotentialPower)),
            ("armor", Number(asked.Armor)),
            ("allocatedTraits", Number(asked.AllocatedTraits)),
            ("hasImbuedTraits", Flag(asked.HasImbuedTraits)),
            ("randomStats", Joined(asked.RandomStats)),
            ("modifiers", Joined(asked.Modifiers?.Select(modifier => modifier.ToString()))),
            ("imbuedTraits", Joined(asked.ImbuedTraits?.Select(trait => trait.ToString()))),
        };

        if (asked.Stats is { Count: > 0 } stats)
        {
            given.AddRange(
                from stat in stats
                from value in stat.Value
                select (stat.Key, (string?)Number(value)));
        }

        if (asked.Parameters is { Count: > 0 } parameters)
        {
            given.AddRange(parameters.Select(entry => (entry.Key, (string?)entry.Value)));
        }

        return Written(address, given);
    }

    /// <summary>The <c>/api</c> route answering entities matching <paramref name="query"/> by name.</summary>
    public static string Search(string query, EntityType? type = null, int? limit = null) =>
        Written(
            new StringBuilder("api/search"),
            [
                ("q", query),
                ("type", type is { } narrowed ? EntityTypes.Slug(narrowed) : null),
                ("limit", Number(limit)),
            ]);

    /// <summary>The <c>/api</c> route answering a filtered page of entities.</summary>
    public static string Entities(EntityFilter? filter = null)
    {
        var given = new List<(string Key, string? Value)>
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
            given.AddRange(facets.Select(entry => (entry.Key, (string?)entry.Value)));
        }

        return Written(new StringBuilder("api/entities"), given);
    }

    /// <summary>The <c>/api</c> route answering the catalogue of everything the build carries.</summary>
    public const string Catalog = "api/catalog";

    private static string Written(
        StringBuilder address, IReadOnlyList<(string Key, string? Value)> given)
    {
        var first = true;

        foreach (var (key, value) in given)
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

    private static string? Joined(IEnumerable<string>? given) =>
        given is null ? null : string.Join(',', given) is { Length: > 0 } written ? written : null;

    private static string? Flag(bool? given) => given is true ? "true" : null;

    private static string? Number(int? given) => given?.ToString(CultureInfo.InvariantCulture);

    private static string Number(int given) => given.ToString(CultureInfo.InvariantCulture);

    private static string? Number(decimal? given) => given?.ToString(CultureInfo.InvariantCulture);

    private static string Number(decimal given) => given.ToString(CultureInfo.InvariantCulture);
}
