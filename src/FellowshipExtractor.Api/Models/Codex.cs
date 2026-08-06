using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public sealed record Facet(
    string Key,
    string Label,
    Func<EntityDocument, IEnumerable<string>> Values)
{
    public Func<DesignTokens, string, string?>? Colour { get; init; }

    public bool Matches(EntityDocument document, IReadOnlyCollection<string> wanted) =>
        Values(document).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}

public sealed record Flag(string Key, string Label, Func<EntityDocument, bool> Predicate);

public sealed record Measure(string Key, string Label, Func<EntityDocument, double?> Value)
{
    public string? Unit { get; init; }

    public bool Column { get; init; }
}

public sealed record FacetValue(string Value, string Label, int Count, string? Colour);

public sealed record FacetView(string Key, string Label, IReadOnlyList<FacetValue> Values);

public sealed record TypeView(EntityType Type, string Slug, string Label, string Group, int Count);

public sealed record MenuLink(string Label, string Query, int Count, string? Colour = null);

public sealed record MenuEntry(string Label, string Query, int Count, IReadOnlyList<MenuSection> Sections);

public sealed record MenuSection(string Label, IReadOnlyList<MenuLink> Links);

public sealed record MenuGroup(string Label, IReadOnlyList<MenuEntry> Entries);

internal static class DocumentFields
{
    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Cache = new();

    private static PropertyInfo? Property(EntityDocument document, string name) =>
        Cache.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property));

    public static double? Number(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) switch
        {
            null => null,
            double number => number,
            int number => number,
            var boxed => throw new InvalidOperationException(
                $"{document.GetType().Name}.{name} is {boxed.GetType().Name}, which DocumentFields.Number does not unbox."),
        };

    public static string? Text(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as string;

    public static IReadOnlyList<string> Words(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as IReadOnlyList<string> ?? [];
}

public sealed class Codex
{
    private const int MenuValueLimit = 12;

    private const int MenuSectionLimit = 4;

    private readonly EntityStore store;
    private readonly DesignTokens tokens;

    private readonly IReadOnlyDictionary<string, string[]> offered;

    public IReadOnlyList<Facet> Facets { get; }

    public IReadOnlyList<Flag> Flags { get; }

    public IReadOnlyList<Measure> Measures { get; }

    public IReadOnlyList<TypeView> Types { get; }

    public int Count => store.Count;

    public IReadOnlyList<MenuGroup> Menu { get; }

    public Codex(EntityStore store, DesignTokens tokens)
    {
        this.store = store;
        this.tokens = tokens;

        Facets =
        [
            new Facet("slot", "Slot", document => One(document is ItemDocument item ? item.Slot : null)),
            new Facet("armor", "Armour", document =>
                One(document is ItemDocument { ArmorType: { } armor } ? armor.ToString() : null)),

            new Facet("stat", "Primary stat", document =>
                One(document is ItemDocument { PrimaryStat: { } stat } ? stat.ToString() : null)),

            new Facet("hero", "Hero", document => document switch
            {
                ItemDocument { Heroes: { } heroes } => heroes.Select(hero => hero.ToString()),
                TalentSlotDocument slot => [slot.Hero.ToString()],
                _ => [],
            })
            {
                Colour = (t, value) => t.Hero(Codename(store, value)),
            },

            new Facet("school", "School", Schools),
            new Facet("set", "Set", document => document is ItemDocument { SetBonus: { } sets } ? sets : []),
            new Facet("resource", "Resource", document =>
                One(document is AbilityDocument { Resource: { } resource } ? resource : null)),

            new Facet("gem", "Gem type", document => One(document switch
            {
                GemTypeDocument gem => gem.Gem.ToString(),
                GemTierDocument tier => tier.Gem.ToString(),
                GemTraitDocument trait => trait.Gem.ToString(),
                _ => null,
            }))
            {
                Colour = (t, value) => t.Gem(value),
            },

            new Facet("trait", "Trait category", document =>
                One(document is TraitDocument { Category: { } category } ? category : null)),

            new Facet("role", "Role", document => document is SetBonusDocument { AllowedRoles: { } roles } ? roles : [])
            {
                Colour = (t, value) => t.Role(value),
            },

            new Facet("rarity", "Rarity", document => One(document switch
            {
                ItemDocument { Rarity: { } rarity } => RarityWord(store, rarity, gem: false),
                CurrencyDocument { Rarity: { } coin } => RarityWord(store, coin, gem: false),
                GemTierDocument tier => RarityWord(store, tier.Rarity, gem: true),
                _ => null,
            }))
            {
                Colour = (t, value) => t.Rarity(Filed(store, value)),
            },
        ];

        Flags =
        [
            new Flag("named", "Named in the game", document => PublicTypes.DisplayNameOf(document) is not null),
            new Flag("icon", "Has art", document => DocumentFields.Text(document, nameof(ItemDocument.Icon)) is not null),
            new Flag("described", "Has description text",
                document => DocumentFields.Text(document, nameof(ItemDocument.Description)) is not null),
            new Flag("grants", "Grants an ability", document => document is ItemDocument { GrantsId: not null }),
            new Flag("socket", "Rolls with a gem socket", document => document is ItemDocument { GemSlot: true }),
            new Flag("unique", "Unique equipped", document => document is ItemDocument { UniqueEquipped: true }),
            new Flag("scaling", "Scales off an attribute",
                document => DocumentFields.Words(document, nameof(AbilityDocument.ScalesOff)).Count > 0),
        ];

        Measures =
        [
            new Measure("cooldown", "Cooldown", document => DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)))
            {
                Unit = "s", Column = true,
            },
            new Measure("cast", "Cast time", document => DocumentFields.Number(document, nameof(AbilityDocument.CastTime)))
            {
                Unit = "s", Column = true,
            },
            new Measure("channel", "Channel", document => DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)))
            {
                Unit = "s",
            },
            new Measure("range", "Range", document => DocumentFields.Number(document, nameof(AbilityDocument.Range)))
            {
                Column = true,
            },
            new Measure("radius", "Radius", document => DocumentFields.Number(document, nameof(AbilityDocument.Radius))),
            new Measure("duration", "Duration", document => DocumentFields.Number(document, nameof(AbilityDocument.Duration)))
            {
                Unit = "s",
            },
            new Measure("cost", "Cost", document => DocumentFields.Number(document, nameof(AbilityDocument.Cost))),
            new Measure("charges", "Charges", document => DocumentFields.Number(document, nameof(AbilityDocument.Charges))),
            new Measure("tier", "Gem tier", document => DocumentFields.Number(document, nameof(GemTierDocument.Tier))),
            new Measure("rank", "Max rank", document => DocumentFields.Number(document, nameof(TraitDocument.MaxRank))),

            new Measure("id", "ID", document => (double?)document.Id) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(g => g.Key, g => g.Count());

        Types =
        [
            .. PublicTypes.All
                .Select(type => new TypeView(
                    type.Type, type.Slug, type.Label, type.Group, counts.GetValueOrDefault(type.Type)))
                .Where(type => type.Count > 0),
        ];

        offered = Facets.ToDictionary(
            facet => facet.Key,
            facet => View(facet, store.Entities).Values.Select(value => value.Value).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        Menu = BuildMenu();
    }

    public IReadOnlyList<EntityDocument> Of(EntityType type) =>
        [.. store.Entities.Where(document => EntityTypes.TypeOf(document) == type)];

    public FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var value in facet.Values(row))
            {
                if (value.Length == 0) continue;
                counted[value] = 1 + counted.GetValueOrDefault(value);
            }
        }

        return new FacetView(facet.Key, facet.Label,
        [
            .. counted
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(spellings => (Value: Written(spellings), Count: spellings.Sum(pair => pair.Value)))
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new FacetValue(
                    value.Value,
                    Printed(facet, value.Value),
                    value.Count,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, value.Value)))),
        ]);
    }

    private static string Written(IEnumerable<KeyValuePair<string, int>> spellings) =>
        spellings
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First()
            .Key;

    public string Printed(Facet facet, string value) => Spaced(value);

    public IReadOnlyList<string> Offered(Facet facet) => offered.GetValueOrDefault(facet.Key, []);

    public Facet? Facet(string key) =>
        Facets.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public Measure? Measure(string key) =>
        Measures.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    public Flag? Flag(string key) =>
        Flags.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<MenuGroup> BuildMenu()
    {
        var groups = new List<MenuGroup>();

        foreach (var group in Types.Select(type => type.Group).Distinct(StringComparer.Ordinal))
        {
            var entries = new List<MenuEntry>();

            foreach (var type in Types.Where(t => t.Group == group))
            {
                var rows = Of(type.Type);
                var sections = new List<MenuSection>();

                foreach (var facet in Facets)
                {
                    var view = View(facet, rows);
                    if (view.Values.Count < 2) continue;

                    sections.Add(new MenuSection(view.Label,
                    [
                        .. view.Values.Take(MenuValueLimit).Select(value => new MenuLink(
                            value.Label,
                            $"?type={Uri.EscapeDataString(type.Slug)}&{facet.Key}={Uri.EscapeDataString(value.Value)}",
                            value.Count,
                            value.Colour)),
                    ]));
                }

                entries.Add(new MenuEntry(
                    type.Label,
                    $"?type={Uri.EscapeDataString(type.Slug)}",
                    type.Count,
                    [.. sections.Take(MenuSectionLimit)]));
            }

            groups.Add(new MenuGroup(group, entries));
        }

        return groups;
    }

    private static IEnumerable<string> One(string? value) => value is null ? [] : [value];

    public static IReadOnlyList<string> Schools(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.Schools ?? [],
        EffectDocument effect => effect.Schools ?? [],
        RelicPowerDocument relic => relic.Schools ?? [],
        WeaponPowerDocument weapon => weapon.Schools ?? [],
        _ => [],
    };

    public IEnumerable<string> Scaling(IReadOnlyList<string>? attributes) =>
        (attributes ?? []).Select(store.StatName).Distinct(StringComparer.Ordinal);

    private static string Codename(EntityStore store, string name) =>
        store.Hero(name)?.Codename ?? name;

    private static string RarityWord(EntityStore store, Rarity rarity, bool gem) =>
        store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == (int)rarity) is { } entry
            ? (gem ? entry.GemDisplayName : entry.DisplayName)
            : rarity.ToString();

    private static string Filed(EntityStore store, string printed) =>
        store.Rarity(printed, gem: true)?.Name ?? printed;

    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }

    public static string Label(EntityType type) => PublicTypes.Label(type);

    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);
}
