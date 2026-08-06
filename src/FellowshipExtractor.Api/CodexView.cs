using System.Globalization;
using System.Reflection;
using System.Text;
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

        return PublicTypes.DisplayNameOf(entity) is null
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

    public string Name(Entity entity) => PublicTypes.NameOf(entity);

    public string Name(string? table, int netId) =>
        store.All(table ?? string.Empty, netId) is { Count: > 0 } readings
            ? PublicTypes.NameOf(readings[0])
            : PublicTypes.NameOf(new Entity { Table = table, NetId = netId });

    public string Href(Entity entity) => Href(entity.Table, entity.NetId ?? 0);

    public string Href(string? table, int netId) =>
        PublicTypes.SlugOf(table) is { } slug
            ? $"/entity/{slug}/{netId.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

    public string Tip(Entity entity)
    {
        if (PublicTypes.SlugOf(entity.Table) is not { } slug) return string.Empty;

        var where = $"/api/tooltip/{slug}/{(entity.NetId ?? 0).ToString(CultureInfo.InvariantCulture)}";

        return Reading(entity) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }

    public string? Reading(Entity entity)
    {
        if (entity.Table is not { } table || entity.NetId is not { } netId) return null;
        if (store.All(table, netId).Count <= 1) return null;

        return store.Wearer(entity)?.Name;
    }

    public MarkupString Text(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags)
    {
        if (source is null) return default;

        return new MarkupString(richText.Render(source, Resolved(tokenValues, tags)));
    }

    public string? PlainText(string? source, IReadOnlyDictionary<string, Placeholder>? tokenValues,
        IReadOnlyList<string>? tags) =>
        source is null ? null : richText.Plain(source, Resolved(tokenValues, tags));

    private IReadOnlyDictionary<string, Substitution>? Resolved(
        IReadOnlyDictionary<string, Placeholder>? tokenValues, IReadOnlyList<string>? tags) =>
        tokenValues is null ? null : placeholders.Resolve(tokenValues, new TooltipQuery(), null, tags);

    public string Reading(Placeholder token, IReadOnlyList<string>? tags) =>
        placeholders.Reading(token, new TooltipQuery(), null, tags).Text;

    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts(Entity entity)
    {
        var facts = new List<(string, string, string?, string?, string?)>();

        foreach (var property in Scalars)
        {
            if (Drawn.Contains(property.Name) || Internal.Contains(property.Name)) continue;

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

            facts.Add((Heading(property.Name), text, Unit(property.Name), points?.Href, points?.Tip));
        }

        return facts;
    }

    private (string Href, string? Tip)? Points(Entity entity, string property, object value) => property switch
    {
        nameof(Entity.Augmentation) when value is int id => Addresses(TalentAsset, id),
        nameof(Entity.Replaces) when value is int id => Addresses(entity.Table, id),
        _ => null,
    };

    public (string Href, string? Tip)? Addresses(string? table, int netId)
    {
        if (!PublicTypes.IsPublic(table)) return null;

        return (Href(table, netId), store.All(table!, netId) is { Count: > 0 } readings ? Tip(readings[0]) : null);
    }

    private static readonly PropertyInfo[] Properties = typeof(Entity)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);

    private static readonly PropertyInfo[] Scalars =
        [.. Properties.Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))];

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal);

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(Entity.Table), nameof(Entity.NetId), nameof(Entity.DisplayName),
        nameof(Entity.Description), nameof(Entity.Icon),
    };

    private static readonly HashSet<string> Internal = new(StringComparer.Ordinal)
    {
        nameof(Entity.Name), nameof(Entity.RuleSet), nameof(Entity.TalentSlot),
        nameof(Entity.TraitCategoryTag), nameof(Entity.GemAttribute),
    };

    private const string TalentAsset = "CRCharacterAugmentationAsset";

    private static string Heading(string property) => property switch
    {
        nameof(Entity.Augmentation) => PublicTypes.Label(TalentAsset),
        _ => Codex.Spaced(property),
    };

    private static string? Unit(string property) => property switch
    {
        nameof(Entity.CastTime) or nameof(Entity.ChannelTime) or nameof(Entity.ChannelTick)
            or nameof(Entity.Cooldown) or nameof(Entity.Duration) or nameof(Entity.Period) => "s",
        _ => null,
    };

    public IReadOnlyList<TuningRow> Tuning(IReadOnlyDictionary<string, object?>? rows)
    {
        if (rows is not { Count: > 0 }) return [];

        var read = rows
            .Select(row => (Path: Knob(row.Key), Value: Constant(row.Value)))
            .Where(row => row.Path.Length > 0)
            .ToList();

        var listed = new List<TuningRow>(read.Count);

        foreach (var row in read)
        {
            var repeats = read.Any(other =>
                other.Path.Length < row.Path.Length &&
                row.Path.EndsWith("." + other.Path, StringComparison.Ordinal) &&
                other.Value == row.Value);

            if (repeats) continue;

            var label = Spaced(row.Path);

            if (listed.Any(other => other.Label == label && other.Value == row.Value)) continue;

            listed.Add(new TuningRow(label, row.Value, row.Value == Absent));
        }

        return listed;
    }

    public string TuningName(string key) => Spaced(Knob(key));

    private static string Spaced(string knob) => Codex.Spaced(knob.Replace('_', ' '));

    private string Knob(string key)
    {
        var segments = key.Split('.');
        var kept = new List<string>(segments.Length);

        for (var at = 0; at < segments.Length; at++)
        {
            var segment = segments[at];

            if (NamesAnAsset(segment) || (at == 0 && segment.Contains('_', StringComparison.Ordinal)))
            {
                continue;
            }

            kept.Add(Renamed(segment));
        }

        return kept.Count > 0 ? string.Join('.', kept) : string.Empty;
    }

    private static bool NamesAnAsset(string segment)
    {
        var underscore = segment.IndexOf('_');
        if (underscore < 2) return false;

        for (var at = 0; at < underscore; at++)
        {
            if (!char.IsUpper(segment[at])) return false;
        }

        return true;
    }

    private string Renamed(string segment)
    {
        if (store.StatDisplay(segment)?.DisplayName is { Length: > 0 } named) return named;

        var words = Words(segment);
        var built = new StringBuilder(segment.Length + 8);

        for (var at = 0; at < words.Length; at++)
        {
            string? printed = null;
            var run = words.Length;

            for (; run > at; run--)
            {
                if (stats.TryGetValue(string.Concat(words[at..run]), out printed)) break;
            }

            if (printed is not null)
            {
                built.Append(printed);
                at = run - 1;
                continue;
            }

            built.Append(heroes.TryGetValue(words[at], out var hero) ? hero : words[at]);
        }

        return built.ToString();
    }

    private static string[] Words(string segment)
    {
        var words = new List<string>();
        var start = 0;

        for (var at = 1; at <= segment.Length; at++)
        {
            if (at < segment.Length && !(char.IsUpper(segment[at]) && !char.IsUpper(segment[at - 1]))) continue;

            words.Add(segment[start..at]);
            start = at;
        }

        return [.. words];
    }

    private readonly IReadOnlyDictionary<string, string> heroes = store.Vocabulary.Heroes
        .ToDictionary(hero => hero.Codename, hero => hero.Name, StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, string> stats = store.Vocabulary.StatCalculation.Attributes
        .Where(row => row.Value.DisplayName is { Length: > 0 } printed &&
            !printed.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(row.Key, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(row => row.Key, row => row.Value.DisplayName!, StringComparer.Ordinal);

    public static string Constant(object? value) => value switch
    {
        null => Absent,
        JsonElement { ValueKind: JsonValueKind.Array } array =>
            string.Join(" / ", array.EnumerateArray().Select(item => Constant(item))),
        JsonElement { ValueKind: JsonValueKind.Number } number => Number(number.GetDouble()),
        JsonElement { ValueKind: JsonValueKind.Null } => Absent,
        JsonElement element => element.ToString(),
        double number => Number(number),
        _ => value.ToString() ?? string.Empty,
    };

    private const string Absent = "not in the build";

    public IReadOnlyList<TokenReading> Substitutions(Entity entity)
    {
        var read = new List<TokenReading>();

        foreach (var token in Spent(Spends(entity), entity.Placeholders))
        {
            var (reading, resolved) = Reads(token);

            read.Add(new TokenReading(read.Count + 1, reading, resolved, Working(token), Tuning(token.Constants)));
        }

        return read;
    }

    public IEnumerable<Placeholder> Spent(string text, IReadOnlyDictionary<string, Placeholder>? tokens) =>
        tokens is not { Count: > 0 }
            ? []
            : tokens
                .OrderBy(spent => Spends(text, spent.Key))
                .ThenBy(spent => spent.Key, StringComparer.Ordinal)
                .Select(spent => spent.Value);

    public static string Spends(Entity entity) =>
        string.Join('\n', new[] { entity.Description }.Concat(entity.Detail ?? []).OfType<string>());

    private static int Spends(string text, string name)
    {
        var at = text.IndexOf($"{{{name}}}", StringComparison.Ordinal);

        return at < 0 ? int.MaxValue : at;
    }

    private (string Reading, bool Resolved) Reads(Placeholder token)
    {
        if (token.Text is { } word) return (word, true);

        if (token.Coefficient is { } coefficient)
        {
            var share = ((decimal)coefficient * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

            var of = token.Attribute switch
            {
                null => "the attribute the row captures",
                Relative => "whichever primary stat the hero uses",
                { } attribute => store.StatName(attribute),
            };

            return ($"{share}% of {of}", true);
        }

        if (token.Value is { } value)
        {
            if (token.Percent == true || token.Format == Share)
            {
                return (((decimal)value * 100m).Normalize().ToString(CultureInfo.InvariantCulture) + "%", true);
            }

            var number = Number(value);

            return (token.Unit is { } unit ? $"{number} {unit}" : number, true);
        }

        var unresolved = Placeholders.Unresolved(token);

        return (unresolved.Title ?? unresolved.Text, false);
    }

    private const string Relative = "PrimaryAttribute";

    private string Working(Placeholder token)
    {
        var parts = new List<string>(4);

        if (token.Spread is { } spread)
        {
            parts.Add(spread == 0
                ? "rolls exactly, with no band"
                : $"rolls between {Number(100 - spread * 50)}% and {Number(100 + spread * 50)}% of the mean");
        }

        if (token.Format is { } format) parts.Add(Printed(format));

        if (token.Value is null && token.Percent == true) parts.Add("shown as a share");

        if (token.ShowDamageType == true) parts.Add("speaks its damage school");

        if (codex.Scaling(token.ScalesOff).ToArray() is { Length: > 0 } scaling)
        {
            parts.Add("scales off " + string.Join(", ", scaling));
        }

        return string.Join(" · ", parts);
    }

    private static string Printed(string format) => format switch
    {
        Plain => "printed as it stands",
        Share => "printed as a share",
        Excess => "printed as its excess over 1",
        _ => $"printed as {Codex.Spaced(format)}",
    };

    private const string Plain = "number";

    private const string Share = "percent";

    private const string Excess = "delta";
}

public sealed record TuningRow(string Label, string Value, bool Absent);

public sealed record TokenReading(
    int Ordinal, string Reading, bool Resolved, string Working, IReadOnlyList<TuningRow> Tuning);
