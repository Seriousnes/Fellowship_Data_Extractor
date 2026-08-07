using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Azure.Storage.Blobs;

using Fellowship.SDK.Documents;

using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api.Models;

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

    public string? LinkColour(EntityDocument document)
    {
        Rarity? rung = document switch
        {
            ItemDocument { Rarities: { Count: > 0 } rungs } => rungs[^1],
            ItemDocument { Rarity: { } stated } => stated,
            GemDocument gem => gem.Rarity,
            _ => null,
        };

        if (rung is { } found && Rung(found) is { } shade) return shade;

        GemType? stone = document switch
        {
            GemTypeDocument type => type.Type,
            GemDocument gem => gem.Type,
            GemTraitDocument trait => trait.Gem,
            _ => null,
        };

        return stone is { } named ? Readable(tokens.Gem(named.ToString())) : null;
    }

    private string? Rung(Rarity rarity) =>
        store.Vocabulary.Rarities.FirstOrDefault(entry => entry.Tier == (int)rarity) is { } rung
            ? Readable(tokens.Rarity(rung.Name))
            : null;

    public string NameColour(EntityDocument document) =>
        LinkColour(document)
        ?? (PublicTypes.DisplayNameOf(document) is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB");

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

    public string Name(EntityDocument document) => PublicTypes.NameOf(document);

    public string Name(EntityType type, int id) => PublicTypes.NameOf(store.All(type, id)[0]);

    public string Href(EntityDocument document) => Href(EntityTypes.TypeOf(document), document.Id);

    public string Href(EntityType type, int id) =>
        $"/entity/{EntityTypes.Slug(type)}/{id.ToString(CultureInfo.InvariantCulture)}";

    public string Tip(EntityDocument document)
    {
        var where = $"/api/tooltip/{EntityTypes.Slug(EntityTypes.TypeOf(document))}/" +
            document.Id.ToString(CultureInfo.InvariantCulture);

        return Reading(document) is { } hero ? $"{where}?hero={Uri.EscapeDataString(hero)}" : where;
    }

    public string? Reading(EntityDocument document) =>
        store.All(EntityTypes.TypeOf(document), document.Id).Count <= 1 ? null : store.Wearer(document)?.Name;

    public MarkupString Text(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags)
    {
        if (source is null) return default;

        return new MarkupString(richText.Render(source, Resolved(tokens, tags)));
    }

    public string? PlainText(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags) =>
        source is null ? null : richText.Plain(source, Resolved(tokens, tags));

    private IReadOnlyDictionary<string, Substitution>? Resolved(
        IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags) =>
        tokens is not { Count: > 0 } ? null : placeholders.Resolve(Keyed(tokens), new TooltipQuery(), null, tags);

    private static IReadOnlyDictionary<string, Placeholder> Keyed(IReadOnlyList<Placeholder> tokens)
    {
        var keyed = new Dictionary<string, Placeholder>(tokens.Count, StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Key is { } key) keyed[key] = token;
        }

        return keyed;
    }

    public static IReadOnlyList<Placeholder> PlaceholdersOf(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.Placeholders ?? [],
        EffectDocument effect => effect.Placeholders ?? [],
        BlessingDocument blessing => blessing.Placeholders ?? [],
        GemTraitDocument trait => trait.Placeholders ?? [],
        TalentDocument talent => talent.Placeholders ?? [],
        TraitPowerDocument power => power.Placeholders ?? [],
        WeaponPowerDocument power => power.Placeholders ?? [],
        _ => [],
    };

    public string Reading(Placeholder token, IReadOnlyList<string>? tags) =>
        placeholders.Reading(token, new TooltipQuery(), null, tags).Text;

    public string RichTextCss(string scope) => richText.Css(scope);

    public static string Number(double value) => Codex.Number(value);

    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, string? Tip)> Facts(
        EntityDocument document)
    {
        var facts = new List<(string, string, string?, string?, string?)>();

        foreach (var property in Scalars(document))
        {
            if (Drawn.Contains(property.Name) || Hidden(document, property.Name)) continue;

            var value = property.GetValue(document);
            if (value is null) continue;

            var text = value switch
            {
                bool flag => flag ? "Yes" : "No",
                double number => Number(number),
                int number => number.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };

            if (text.Length == 0) continue;

            var points = Points(document, property.Name, value);

            facts.Add((Heading(document, property.Name), text, Unit(property.Name), points?.Href, points?.Tip));
        }

        return facts;
    }

    private (string Href, string? Tip)? Points(EntityDocument document, string property, object value) =>
        document switch
        {
            TraitDocument when property == nameof(TraitDocument.PowerId) && value is int id => Addresses(EntityType.TraitPower, id),
            SetBonusDocument when property == nameof(SetBonusDocument.PowerId) && value is int id =>
                Addresses(EntityType.SetBonusPower, id),
            TalentSlotDocument when property == nameof(TalentSlotDocument.TalentId) && value is int id =>
                Addresses(EntityType.Talent, id),
            GemTraitDocument when property == nameof(GemTraitDocument.ReplacesId) && value is int id =>
                Addresses(EntityType.GemTrait, id),
            ItemDocument when property == nameof(ItemDocument.GrantsId) && value is int id && Power(id) is { } found =>
                Addresses(found.Type, found.Id),
            _ => null,
        };

    private (EntityType Type, int Id)? Power(int id) =>
        store.All(EntityType.RelicPower, id).Count > 0 ? (EntityType.RelicPower, id)
        : store.All(EntityType.WeaponPower, id).Count > 0 ? (EntityType.WeaponPower, id)
        : null;

    public (string Href, string? Tip)? Addresses(EntityType type, int id) =>
        store.All(type, id) is { Count: > 0 } readings ? (Href(type, id), Tip(readings[0])) : null;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ScalarsByType = new();

    private static PropertyInfo[] Scalars(EntityDocument document) =>
        ScalarsByType.GetOrAdd(document.GetType(), type =>
        [
            .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)),
        ]);

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal) || type.IsEnum;

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal)
    {
        nameof(EntityDocument.Id), nameof(EntityDocument.Name),
        nameof(AbilityDocument.Description), nameof(ItemDocument.Icon), nameof(GemTypeDocument.OverflowDescription),
    };

    private static bool Hidden(EntityDocument document, string property) => document switch
    {
        GemTypeDocument => property == nameof(GemTypeDocument.Attribute),
        ItemDocument => property is nameof(ItemDocument.Rarity) or nameof(ItemDocument.Rarities)
            or nameof(ItemDocument.RuleSet),
        CurrencyDocument => property == nameof(CurrencyDocument.Rarity),
        GemDocument => property == nameof(GemDocument.Rarity),
        _ => false,
    };

    private static string Heading(EntityDocument document, string property) => document switch
    {
        TraitDocument when property == nameof(TraitDocument.PowerId) => PublicTypes.Label(EntityType.TraitPower),
        SetBonusDocument when property == nameof(SetBonusDocument.PowerId) => PublicTypes.Label(EntityType.SetBonusPower),
        TalentSlotDocument when property == nameof(TalentSlotDocument.TalentId) => PublicTypes.Label(EntityType.Talent),
        GemTraitDocument when property == nameof(GemTraitDocument.ReplacesId) => PublicTypes.Label(EntityType.GemTrait),
        _ => Codex.Spaced(property),
    };

    private static string? Unit(string property) => property switch
    {
        nameof(AbilityDocument.CastTime) or nameof(AbilityDocument.ChannelTime) or nameof(AbilityDocument.ChannelTick)
            or nameof(AbilityDocument.Cooldown) or nameof(AbilityDocument.Duration) or nameof(AbilityDocument.Period) => "s",
        _ => null,
    };

    public string? Rarity(EntityDocument document)
    {
        Rarity? rung = document switch
        {
            ItemDocument { Rarity: { } value } => value,
            CurrencyDocument { Rarity: { } coin } => coin,
            GemDocument gem => gem.Rarity,
            _ => null,
        };

        if (rung is not { } tier2) return null;

        var entry = store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == (int)tier2);

        return entry is null ? tier2.ToString() : document is GemDocument ? entry.GemDisplayName : entry.DisplayName;
    }

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

    public IReadOnlyList<TokenReading> Substitutions(EntityDocument document)
    {
        var tokens = PlaceholdersOf(document);
        var read = new List<TokenReading>(tokens.Count);

        foreach (var token in tokens)
        {
            var (reading, resolved) = Reads(token);

            read.Add(new TokenReading(read.Count + 1, reading, resolved, Working(token), Tuning(token.Constants)));
        }

        return read;
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
