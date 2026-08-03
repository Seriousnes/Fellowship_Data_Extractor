using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api;

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

    public string Icon(string? icon) => icon is null ? chrome.SlotPlate : $"{icons.Uri}/{icon}";

    public bool HasIcon(string? icon) => icon is not null;

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

    public string? HeroColour(string name) => Readable(tokens.Hero(store.Hero(name)?.Codename ?? name));

    public string? HeroSwatch(string name) => tokens.Hero(store.Hero(name)?.Codename ?? name);

    public static string? Readable(string? colour)
    {
        if (colour is null || colour.Length != 7 || colour[0] != '#') return colour;

        if (!int.TryParse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return colour;
        }

        double r = (packed >> 16) & 0xFF, g = (packed >> 8) & 0xFF, b = packed & 0xFF;

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

    private const double Floor = 0.16;

    private static double Luminance(double r, double g, double b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(double value)
    {
        var scaled = value / 255d;
        return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
    }

    public string Name(Entity entity) => entity.DisplayName ?? entity.Name ?? $"{entity.Table} {entity.NetId}";

    public string Href(Entity entity) => Href(entity.Table!, entity.NetId ?? 0);

    public string Href(string table, int netId) =>
        $"/entity/{Uri.EscapeDataString(table)}/{netId.ToString(CultureInfo.InvariantCulture)}";

    public string Tip(Entity entity)
    {
        var where = $"/api/tooltip/{Uri.EscapeDataString(entity.Table ?? string.Empty)}/" +
            (entity.NetId ?? 0).ToString(CultureInfo.InvariantCulture);

        return Reading(entity) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }

    public string? Reading(Entity entity)
    {
        if (entity.Table is not { } table || entity.NetId is not { } netId) return null;
        if (store.All(table, netId).Count <= 1) return null;

        return store.Vocabulary.Heroes.FirstOrDefault(hero =>
            (entity.Heroes is null || entity.Heroes.Contains(hero.Name)) &&
            (entity.ArmorType is null || entity.ArmorType == hero.ArmorType) &&
            (entity.PrimaryStat is null || entity.PrimaryStat == hero.PrimaryStat))?.Name;
    }

    public MarkupString Text(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags)
    {
        if (source is null) return default;

        var resolved = tokenValues is null
            ? null
            : placeholders.Resolve(tokenValues, new TooltipQuery(), null, tags);

        return new MarkupString(richText.Render(source, resolved));
    }

    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

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

    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts(Entity entity)
    {
        var facts = new List<(string, string, string?, string?, string?)>();

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

            var points = Points(entity, property.Name, value);

            facts.Add((
                Codex.Spaced(property.Name), text, Unit(property.Name), points?.Href, points?.Tip));
        }

        return facts;
    }

    private (string Href, string? Tip)? Points(Entity entity, string property, object value) => property switch
    {
        nameof(Entity.Augmentation) when value is int id => Addresses("CRCharacterAugmentationAsset", id),
        nameof(Entity.Replaces) when value is int id && entity.Table is { } table => Addresses(table, id),
        _ => null,
    };

    public (string Href, string? Tip) Addresses(string table, int netId) =>
        (Href(table, netId),
            store.All(table, netId) is { Count: > 0 } readings ? Tip(readings[0]) : null);

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

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(Entity.Table), nameof(Entity.NetId), nameof(Entity.Name), nameof(Entity.DisplayName),
        nameof(Entity.Description), nameof(Entity.Icon),
    };

    private static readonly HashSet<string> Covered = new(StringComparer.Ordinal)
    {
        nameof(Entity.Tags), nameof(Entity.Heroes), nameof(Entity.Stats), nameof(Entity.ScalesOff),
        nameof(Entity.Placeholders), nameof(Entity.Constants), nameof(Entity.Talents),
        nameof(Entity.TalentGates), nameof(Entity.GrantedTags), nameof(Entity.SetBonus),
        nameof(Entity.Detail), nameof(Entity.Grants), nameof(Entity.LegendaryStats),
        nameof(Entity.RankRuns), nameof(Entity.GemOverflow), nameof(Entity.IconInheritedFrom),
        nameof(Entity.AllowedRoles), nameof(Entity.AllowedAttributes), nameof(Entity.Variants),
    };

    private static string? Unit(string property) => property switch
    {
        nameof(Entity.CastTime) or nameof(Entity.ChannelTime) or nameof(Entity.ChannelTick)
            or nameof(Entity.Cooldown) or nameof(Entity.Duration) or nameof(Entity.Period) => "s",
        _ => null,
    };
}
