using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;

using Microsoft.AspNetCore.Components;

using Fellowship.Codex.Models;

using static Fellowship.Codex.Models.CodexView;

namespace Fellowship.Codex.Services;

public sealed record TooltipLink(string? Address, string? Body);

public interface ICodexViewService
{
    IEntityStoreService Store { get; }

    ICodexService Codex { get; }

    IDesignTokenService Tokens { get; }

    IChromeAssetService Chrome { get; }

    [return: NotNullIfNotNull(nameof(icon))]
    string? Icon(string? icon);

    string? Icon(EntityDocument document);

    string? RarityName(EntityDocument document);

    string? RarityName(Rarity? rarity);

    RadianceDocument? Stone(EntityDocument document);

    string? LinkColour(EntityDocument document);

    string NameColour(EntityDocument document);

    string? HeroColour(string name);

    string? HeroFill(string name);

    string? HeroInk(string name);

    HeroDocument? HeroNamed(string name);

    string Name(EntityDocument document);

    string Name(EntityType type, int id);

    string Href(EntityDocument document);

    string Href(EntityDocument document, string? hero);

    string Href(EntityType type, int id);

    string Path(EntityDocument document);

    string? AddressedHero(EntityDocument document, string? hero);

    TooltipLink Tip(EntityDocument document, bool showIcon = false);

    string? Reading(EntityDocument document);

    MarkupString Text(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags);

    string? PlainText(string? source, IReadOnlyList<Placeholder>? tokens, IReadOnlyList<string>? tags);

    string Reading(Placeholder token, IReadOnlyList<string>? tags);

    string RichTextCss(string scope);

    IReadOnlyList<(string Label, string Value, string? Unit, string? Href, TooltipLink? Tip)> Facts(
        EntityDocument document);

    (string Href, TooltipLink? Tip)? Addresses(EntityType type, int id, bool showIcon = false);

    string? Rarity(EntityDocument document);

    IReadOnlyList<TuningRow> Tuning(IReadOnlyDictionary<string, object?>? rows);

    IReadOnlyList<TokenReading> Substitutions(EntityDocument document);
}

public sealed class CodexViewService(
    IEntityStoreService store,
    ICodexService codex,
    IDesignTokenService tokens,
    IChromeAssetService chrome,
    IRichTextService richText,
    IPlaceholderService placeholders) : ICodexViewService
{
    public IEntityStoreService Store => store;

    public ICodexService Codex => codex;

    public IDesignTokenService Tokens => tokens;

    public IChromeAssetService Chrome => chrome;

    [return: NotNullIfNotNull(nameof(icon))]
    public string? Icon(string? icon) => chrome.Texture(icon);

    public string? Icon(EntityDocument document) =>
        Icon(document is HeroDocument hero ? hero.Portrait : DocumentFields.Text(document, nameof(ItemDocument.Icon)));

    public string? RarityName(EntityDocument document) => Tier(document) is { } tier ? Named(tier) : null;

    public string? RarityName(Rarity? rarity) => rarity is { } tier ? Named(tier) : null;

    private string? Named(Rarity rarity) =>
        store.Settings.Rarities.FirstOrDefault(entry => entry.Tier == (int)rarity)?.Name;

    public string? LinkColour(EntityDocument document)
    {
        if (document is HeroDocument { Name: { } hero }) return HeroColour(hero);

        var tier = Tier(document);

        if (tier is { } found && RarityColour(found) is { } shade) return shade;

        return Cut(document) is { } named ? Readable(tokens.Gem(named.ToString())) : null;
    }

    private static GemType? Cut(EntityDocument document) => document switch
    {
        RadianceDocument type => type.Type,
        GemDocument gem => gem.Type,
        GemAbilityDocument augmentation => augmentation.Gem,
        _ => null,
    };

    private readonly ConcurrentDictionary<GemType, RadianceDocument?> stones = new();

    public RadianceDocument? Stone(EntityDocument document) =>
        Cut(document) is { } cut
            ? stones.GetOrAdd(cut, wanted =>
                store.Entities.OfType<RadianceDocument>().FirstOrDefault(type => type.Type == wanted))
            : null;

    private string? RarityColour(Rarity rarity) =>
        store.Settings.Rarities.FirstOrDefault(entry => entry.Tier == (int)rarity) is { } found
            ? Readable(tokens.Rarity(found.Name))
            : null;

    public string NameColour(EntityDocument document) =>
        LinkColour(document)
        ?? (PublicTypes.DisplayNameOf(document) is null
            ? tokens.Color("core.text-secondary") ?? "#909BA6"
            : tokens.Color("core.text-primary") ?? "#EAE3DB");

    public string? HeroColour(string name) => Readable(tokens.Hero(name));

    public string? HeroFill(string name) => tokens.Hero(name);

    public string? HeroInk(string name) => Ink(tokens.Hero(name));

    private readonly ConcurrentDictionary<string, HeroDocument?> heroes = new(StringComparer.OrdinalIgnoreCase);

    public HeroDocument? HeroNamed(string name) =>
        heroes.GetOrAdd(name, wanted => store.Entities
            .OfType<HeroDocument>()
            .FirstOrDefault(hero => string.Equals(hero.Name, wanted, StringComparison.OrdinalIgnoreCase)));

    public string Name(EntityDocument document) => PublicTypes.NameOf(document);

    public string Name(EntityType type, int id) => PublicTypes.NameOf(store.All(type, id)[0]);

    public string Href(EntityDocument document) => Href(document, Reading(document));

    public string Href(EntityDocument document, string? hero) =>
        AddressedHero(document, hero) is { } addressed
            ? $"{Path(document)}?hero={Uri.EscapeDataString(addressed)}"
            : Path(document);

    public string Href(EntityType type, int id) =>
        store.All(type, id) is [var first, ..] ? Path(first) : Bare(type, id);

    public string Path(EntityDocument document) =>
        $"{Bare(EntityTypes.TypeOf(document), document.Id)}/{PublicTypes.Segment(document)}";

    public string? AddressedHero(EntityDocument document, string? hero)
    {
        if (hero is null) return null;

        var segment = PublicTypes.Segment(document);
        var named = store.All(EntityTypes.TypeOf(document), document.Id)
            .First(reading => PublicTypes.Segment(reading) == segment);

        return named == document && Reading(named) == hero ? null : hero;
    }

    private static string Bare(EntityType type, int id) =>
        $"/{EntityTypes.Slug(type)}/{id.ToString(CultureInfo.InvariantCulture)}";

    public TooltipLink Tip(EntityDocument document, bool showIcon = false)
    {
        var type = EntityTypes.TypeOf(document);
        var address = PublicTypes.IsListed(type) ? "/" + CodexAddresses.Tooltip(type, document.Id) : null;

        var icon = showIcon ? true : (bool?)null;
        var hero = Reading(document);

        return new TooltipLink(address, type switch
        {
            EntityType.Ability => Body(new AbilityTooltipRequest { ShowIcon = icon, Hero = hero }),
            EntityType.Glyph => Body(new GlyphTooltipRequest { ShowIcon = icon }),
            EntityType.CompanionPet => Body(new CompanionPetTooltipRequest { ShowIcon = icon }),
            EntityType.Creature => Body(new CreatureTooltipRequest { ShowIcon = icon }),
            EntityType.Currency => Body(new CurrencyTooltipRequest { ShowIcon = icon }),
            EntityType.Dungeon => Body(new DungeonTooltipRequest { ShowIcon = icon }),
            EntityType.Effect => Body(new EffectTooltipRequest { ShowIcon = icon, Hero = hero }),
            EntityType.Gem => Body(new GemTooltipRequest { ShowIcon = icon }),
            EntityType.GemAbility => Body(new GemAbilityTooltipRequest { ShowIcon = icon }),
            EntityType.Item => Body(new ItemTooltipRequest { ShowIcon = icon, Hero = hero }),
            EntityType.LegendaryPower => Body(new LegendaryPowerTooltipRequest { ShowIcon = icon }),
            EntityType.RelicPassive => Body(new RelicPassiveTooltipRequest { ShowIcon = icon }),
            EntityType.Mount => Body(new MountTooltipRequest { ShowIcon = icon }),
            EntityType.RelicPower => Body(new RelicPowerTooltipRequest { ShowIcon = icon }),
            EntityType.Talent => Body(new TalentTooltipRequest { ShowIcon = icon }),
            EntityType.Enchant => Body(new EnchantTooltipRequest { ShowIcon = icon, Hero = hero }),
            EntityType.Sigil => Body(new SigilTooltipRequest { ShowIcon = icon }),
            EntityType.Aspect => Body(new AspectTooltipRequest { ShowIcon = icon }),
            EntityType.ProtectiveEnchant => Body(new ProtectiveEnchantTooltipRequest { ShowIcon = icon }),
            EntityType.WeaponPower => Body(new WeaponPowerTooltipRequest { ShowIcon = icon, Hero = hero }),
            _ => null,
        });
    }

    private static string? Body<TRequest>(TRequest request)
        where TRequest : class
    {
        var body = JsonSerializer.Serialize(request, CodexJson.Options);

        return body == "{}" ? null : body;
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

    public string Reading(Placeholder token, IReadOnlyList<string>? tags) =>
        placeholders.Reading(token, new TooltipQuery(), null, tags).Text;

    public string RichTextCss(string scope) => richText.Css(scope);

    public IReadOnlyList<(string Label, string Value, string? Unit, string? Href, TooltipLink? Tip)> Facts(
        EntityDocument document)
    {
        var facts = new List<(string, string, string?, string?, TooltipLink?)>();

        foreach (var property in Scalars(document))
        {
            if (Excluded.Contains(property.Name) || Hidden(document, property.Name)) continue;

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

            facts.Add((Models.FellowshipCodex.Spaced(property.Name), text, Unit(property.Name), null, null));
        }

        return facts;
    }

    public (string Href, TooltipLink? Tip)? Addresses(EntityType type, int id, bool showIcon = false) =>
        store.All(type, id) is { Count: > 0 } readings
            ? (Href(type, id), Tip(readings[0], showIcon))
            : null;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ScalarsByType = new();

    private static PropertyInfo[] Scalars(EntityDocument document) =>
        ScalarsByType.GetOrAdd(document.GetType(), type =>
        [
            .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => IsScalar(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)),
        ]);

    private static bool IsScalar(Type type) =>
        type == typeof(string) || type.IsPrimitive || type == typeof(decimal) || type.IsEnum;

    private static readonly HashSet<string> Excluded = new(StringComparer.Ordinal)
    {
        nameof(EntityDocument.Id), nameof(EntityDocument.Name),
        nameof(AbilityDocument.Description), nameof(ItemDocument.Icon), nameof(RadianceDocument.OverflowDescription),
        nameof(DungeonDocument.Background),
    };

    private static bool Hidden(EntityDocument document, string property) => document switch
    {
        CreatureDocument => property is nameof(CreatureDocument.Health)
            or nameof(CreatureDocument.Armor) or nameof(CreatureDocument.Resist)
            or nameof(CreatureDocument.Intellect) or nameof(CreatureDocument.Strength)
            or nameof(CreatureDocument.Agility),
        RadianceDocument => property == nameof(RadianceDocument.Attribute),
        ItemDocument => property is nameof(ItemDocument.Rarity) or nameof(ItemDocument.Rarities)
            or nameof(ItemDocument.RuleSet) or nameof(ItemDocument.AttunementTree),
        CurrencyDocument => property == nameof(CurrencyDocument.Rarity),
        MountDocument => property == nameof(MountDocument.Rarity),
        AugmentationDocument => property == nameof(AugmentationDocument.CategoryTag),
        GemDocument => property == nameof(GemDocument.Rarity),
        HeroDocument => property is nameof(HeroDocument.Title) or nameof(HeroDocument.Color)
            or nameof(HeroDocument.Biography) or nameof(HeroDocument.Playstyle)
            or nameof(HeroDocument.SpiritAbility) or nameof(HeroDocument.Portrait)
            or nameof(HeroDocument.RoleIcon),
        _ => false,
    };

    private static string? Unit(string property) => property switch
    {
        nameof(AbilityDocument.CastTime) or nameof(AbilityDocument.ChannelTime) or nameof(AbilityDocument.ChannelTick)
            or nameof(AbilityDocument.Cooldown) or nameof(AbilityDocument.Duration) or nameof(AbilityDocument.Period)
            or nameof(DungeonDocument.QualifierTime) => "s",
        _ => null,
    };

    public string? Rarity(EntityDocument document)
    {
        Rarity? tier = document switch
        {
            ItemDocument { Rarity: { } value } => value,
            CurrencyDocument { Rarity: { } coin } => coin,
            MountDocument { Rarity: { } mount } => mount,
            GemDocument gem => gem.Rarity,
            _ => null,
        };

        if (tier is not { } found) return null;

        var entry = store.Settings.Rarities.FirstOrDefault(r => r.Tier == (int)found);

        return entry is null ? found.ToString() : document is GemDocument ? entry.GemDisplayName : entry.DisplayName;
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

    private static string Spaced(string knob) => Models.FellowshipCodex.Spaced(knob.Replace('_', ' '));

    private string Knob(string key)
    {
        var segments = key.Split('.');
        var parts = new List<string>(segments.Length);

        for (var at = 0; at < segments.Length; at++)
        {
            var segment = segments[at];

            if (NamesAnAsset(segment) || (at == 0 && segment.Contains('_', StringComparison.Ordinal)))
            {
                continue;
            }

            parts.Add(Renamed(segment));
        }

        return parts.Count > 0 ? string.Join('.', parts) : string.Empty;
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

            built.Append(words[at]);
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

    private readonly IReadOnlyDictionary<string, string> stats = store.Settings.StatCalculation.Attributes
        .Where(row => row.Value.DisplayName is { Length: > 0 } printed &&
            !printed.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(row.Key, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(row => row.Key, row => row.Value.DisplayName!, StringComparer.Ordinal);

    public IReadOnlyList<TokenReading> Substitutions(EntityDocument document)
    {
        var tokens = PlaceholdersOf(document);
        var read = new List<TokenReading>(tokens.Count);

        foreach (var token in tokens)
        {
            var (reading, resolved) = Reads(token);

            read.Add(new TokenReading(read.Count + 1, reading, resolved));
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
            if (token.Format == Excess)
            {
                var excess = (decimal)value - 1m;
                var printed = (excess * 100m).Normalize().ToString(CultureInfo.InvariantCulture) + "%";

                return (excess < 0 ? printed : "+" + printed, true);
            }

            if (token.Format == Removed)
            {
                var removed = 1m - (decimal)value;

                return ((removed * 100m).Normalize().ToString(CultureInfo.InvariantCulture) + "%", true);
            }

            if (token.Percent == true || token.Format == Share)
            {
                var share = token.Multiplies == true ? (decimal)value - 1m : (decimal)value;

                return ((share * 100m).Normalize().ToString(CultureInfo.InvariantCulture) + "%", true);
            }

            var number = Number(value);

            return (token.Unit is { } unit ? $"{number} {unit}" : number, true);
        }

        var unresolved = PlaceholderService.Unresolved(token);

        return (unresolved.Title ?? unresolved.Text, false);
    }

    private const string Relative = "PrimaryAttribute";

    private const string Share = "percent";

    private const string Excess = "delta";

    private const string Removed = "reduction";
}
