using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Html;

namespace FellowshipExtractor.Api;

/// <summary>
/// What the browser's pages ask when they need a URL, a colour or a piece of the game's own text.
///
/// It exists so a page never spells out where an icon lives, what colour a rarity is or how a
/// description's markup is rendered. All three are already decided - by the blob container, by the
/// shipped design system and by the rich text renderer - and a page that repeated any of them would
/// be a second answer able to disagree with the tooltip beside it.
/// </summary>
public sealed class CodexView(
    EntityStore store,
    Codex codex,
    DesignTokens tokens,
    ChromeAssets chrome,
    RichTextRenderer richText,
    Placeholders placeholders,
    BlobContainerClient icons)
{
    public EntityStore Store => store;

    public Codex Codex => codex;

    public DesignTokens Tokens => tokens;

    public ChromeAssets Chrome => chrome;

    /// <summary>An exported icon by file name, or the empty slot plate where a row ships no art.</summary>
    public string Icon(string? icon) => icon is null ? chrome.SlotPlate : $"{icons.Uri}/{icon}";

    public bool HasIcon(string? icon) => icon is not null;

    /// <summary>
    /// The colour a row's name is written in. A rarity where the row declares one, the hero's colour
    /// where the row belongs to exactly one hero, and the body colour otherwise. Items do not declare
    /// a rarity - the rung is decided when the item drops - so most rows are deliberately uncoloured
    /// rather than given a rung they never claimed.
    /// </summary>
    public string NameColour(Entity entity)
    {
        if (entity.Rarity is { } rarity && tokens.Rarity(rarity) is { } rung) return Readable(rung)!;
        if (entity.GemType is { } gem && tokens.Gem(gem) is { } stone) return Readable(stone)!;

        if (entity.Heroes is { Count: 1 } && store.Hero(entity.Heroes[0]) is { } hero &&
            tokens.Hero(hero.Codename) is { } colour)
        {
            return Readable(colour)!;
        }

        return entity.DisplayName is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB";
    }

    /// <summary>A hero's colour as text, and as the true colour for a swatch beside it.</summary>
    public string? HeroColour(string name) => Readable(tokens.Hero(store.Hero(name)?.Codename ?? name));

    public string? HeroSwatch(string name) => tokens.Hero(store.Hero(name)?.Codename ?? name);

    /// <summary>
    /// One of the game's colours, lifted until it can be read as text on the page's own ground.
    ///
    /// The palette is followed everywhere else, and this is the one place it cannot be followed
    /// literally: the client draws a hero's colour on lit 3D and on bright panels, so Gunde's
    /// #5A1F20 and Sune's #077365 are legible there and vanish on #0C1722. The hue and the relation
    /// between the twelve are kept - the colour is mixed toward white, never replaced - and the
    /// shipped value is still what a swatch beside the name is filled with, so nothing here hides
    /// what the build says.
    /// </summary>
    public static string? Readable(string? colour)
    {
        if (colour is null || colour.Length != 7 || colour[0] != '#') return colour;

        if (!int.TryParse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return colour;
        }

        double r = (packed >> 16) & 0xFF, g = (packed >> 8) & 0xFF, b = packed & 0xFF;

        // Mixed in tenths rather than solved for, because the target is legibility rather than a
        // number: four of the twelve hero colours need a lift and none needs more than half.
        for (var mix = 0; mix <= 10; mix++)
        {
            var k = mix / 10d;
            double lr = r + (255 - r) * k, lg = g + (255 - g) * k, lb = b + (255 - b) * k;

            if (Luminance(lr, lg, lb) >= Floor || mix == 10)
            {
                return $"#{(int)Math.Round(lr):X2}{(int)Math.Round(lg):X2}{(int)Math.Round(lb):X2}";
            }
        }

        return colour;
    }

    /// <summary>
    /// The relative luminance a coloured name has to clear against the page ground. The ground is
    /// #0C1722 at luminance 0.008, and 0.16 is where a 4.5:1 contrast ratio lands against it.
    /// </summary>
    private const double Floor = 0.16;

    private static double Luminance(double r, double g, double b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(double value)
    {
        var scaled = value / 255d;
        return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
    }

    public string Name(Entity entity) => entity.DisplayName ?? entity.Name ?? $"{entity.Table} {entity.NetId}";

    /// <summary>The address of one entity's page. Every link in the browser is built here.</summary>
    public string Href(Entity entity) => Href(entity.Table!, entity.NetId ?? 0);

    public string Href(string table, int netId) =>
        $"/entity/{Uri.EscapeDataString(table)}/{netId.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// One piece of the game's description text as HTML, with its tokens resolved the same way the
    /// tooltip resolves them. The tags decide whether a damage number is spoken as physical or magic,
    /// so they come from whichever entity owns the sentence.
    /// </summary>
    public HtmlString Text(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags)
    {
        if (source is null) return HtmlString.Empty;

        var resolved = tokenValues is null
            ? null
            : placeholders.Resolve(tokenValues, new TooltipQuery(), null, tags);

        return new HtmlString(richText.Render(source, resolved));
    }

    /// <summary>The rich text style rules, scoped to the class the browser wraps prose in.</summary>
    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

    /// <summary>
    /// A value out of a constants map, which holds either one number, the four a rank progression
    /// carries, or null where the asset named a row the shipped tables do not define.
    /// </summary>
    public static string Constant(object? value) => value switch
    {
        null => "not in the build",
        JsonElement { ValueKind: JsonValueKind.Array } array =>
            string.Join(" / ", array.EnumerateArray().Select(item => Constant(item))),
        JsonElement { ValueKind: JsonValueKind.Number } number => Number(number.GetDouble()),
        JsonElement { ValueKind: JsonValueKind.Null } => "not in the build",
        JsonElement element => element.ToString(),
        double number => Number(number),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// The scalar facts a record carries, in declaration order, read off the record itself rather
    /// than listed here. A field the extractor adds to <see cref="Entity"/> shows up on the page the
    /// next time the API is built, which is the only way a details table can be trusted to be
    /// complete.
    ///
    /// Identity and the fields the page draws in their own place are left out by name, because they
    /// are already on the page: repeating the display name in a table under the title is noise.
    /// </summary>
    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href)> Facts(Entity entity)
    {
        var facts = new List<(string, string, string?, string?)>();

        foreach (var property in Scalars)
        {
            if (Drawn.Contains(property.Name)) continue;

            var value = property.GetValue(entity);
            if (value is null) continue;

            var text = value switch
            {
                bool flag => flag ? "Yes" : "No",
                double number => Number(number),
                int number => number.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            if (text.Length == 0) continue;

            facts.Add((Codex.Spaced(property.Name), text, Unit(property.Name), Points(entity, property.Name, value)));
        }

        return facts;
    }

    /// <summary>
    /// The page a fact's number addresses, for the two scalars that are network ids rather than
    /// quantities. Both are on the record as bare integers because the export never carries a path,
    /// and a reader shown "Augmentation 388" with no way to reach 388 has been told nothing.
    /// </summary>
    private string? Points(Entity entity, string property, object value) => property switch
    {
        nameof(Entity.Augmentation) when value is int id => Href("CRCharacterAugmentationAsset", id),
        nameof(Entity.Replaces) when value is int id && entity.Table is { } table => Href(table, id),
        _ => null,
    };

    /// <summary>
    /// Anything on the record this page has no section for, as the JSON the export wrote. Empty in
    /// a build whose pages cover every field, and the check that keeps it that way: a field added to
    /// <see cref="Entity"/> surfaces here rather than disappearing.
    /// </summary>
    public IReadOnlyList<(string Label, string Json)> Uncovered(Entity entity)
    {
        var left = new List<(string, string)>();

        foreach (var property in Complex)
        {
            if (Covered.Contains(property.Name)) continue;

            var value = property.GetValue(entity);
            if (value is null) continue;

            left.Add((Codex.Spaced(property.Name), JsonSerializer.Serialize(value, Pretty)));
        }

        return left;
    }

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly PropertyInfo[] Properties = typeof(Entity)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static readonly PropertyInfo[] Scalars =
        [.. Properties.Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))];

    private static readonly PropertyInfo[] Complex =
        [.. Properties.Where(p => !IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))];

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal);

    /// <summary>Scalars the page already puts somewhere of its own.</summary>
    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(Entity.Table), nameof(Entity.NetId), nameof(Entity.Name), nameof(Entity.DisplayName),
        nameof(Entity.Description), nameof(Entity.Icon),
    };

    /// <summary>Collections and nested records the page draws in a section of its own.</summary>
    private static readonly HashSet<string> Covered = new(StringComparer.Ordinal)
    {
        nameof(Entity.Tags), nameof(Entity.Heroes), nameof(Entity.Stats), nameof(Entity.ScalesOff),
        nameof(Entity.Placeholders), nameof(Entity.Constants), nameof(Entity.Talents),
        nameof(Entity.TalentGates), nameof(Entity.GrantedTags), nameof(Entity.SetBonus),
        nameof(Entity.Detail), nameof(Entity.Grants), nameof(Entity.LegendaryStats),
        nameof(Entity.RankRuns), nameof(Entity.GemOverflow), nameof(Entity.IconInheritedFrom),
        nameof(Entity.AllowedRoles), nameof(Entity.AllowedAttributes), nameof(Entity.Variants),
    };

    /// <summary>The unit a field is measured in, where the export's own documentation gives one.</summary>
    private static string? Unit(string property) => property switch
    {
        nameof(Entity.CastTime) or nameof(Entity.ChannelTime) or nameof(Entity.ChannelTick)
            or nameof(Entity.Cooldown) or nameof(Entity.Duration) or nameof(Entity.Period) => "s",
        _ => null,
    };
}
