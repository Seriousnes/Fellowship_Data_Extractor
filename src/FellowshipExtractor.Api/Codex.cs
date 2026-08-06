using System.Globalization;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed record Facet(
    string Key,
    string Label,
    Func<Entity, IEnumerable<string>> Values)
{
    public Func<DesignTokens, string, string?>? Colour { get; init; }

    public Func<EntityStore, string, string>? Display { get; init; }

    public bool Matches(Entity entity, IReadOnlyCollection<string> wanted) =>
        Values(entity).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}

public sealed record Flag(string Key, string Label, Func<Entity, bool> Predicate);

public sealed record Measure(string Key, string Label, Func<Entity, double?> Value)
{
    public string? Unit { get; init; }

    public bool Column { get; init; }
}

public sealed record FacetValue(string Value, string Label, int Count, string? Colour);

public sealed record FacetView(string Key, string Label, IReadOnlyList<FacetValue> Values);

public sealed record EntityType(string Table, string Slug, string Label, string Group, int Count);

public sealed record MenuLink(string Label, string Query, int Count, string? Colour = null);

public sealed record MenuEntry(string Label, string Query, int Count, IReadOnlyList<MenuSection> Sections);

public sealed record MenuSection(string Label, IReadOnlyList<MenuLink> Links);

public sealed record MenuGroup(string Label, IReadOnlyList<MenuEntry> Entries);

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

    public IReadOnlyList<EntityType> Types { get; }

    public int Count => store.PublicCount;

    public IReadOnlyList<MenuGroup> Menu { get; }

    public Codex(EntityStore store, DesignTokens tokens)
    {
        this.store = store;
        this.tokens = tokens;

        Facets =
        [
            new Facet("slot", "Slot", e => One(e.Slot)),
            new Facet("armor", "Armour", e => One(e.ArmorType)),
            new Facet("stat", "Primary stat", e =>
                One(e.PrimaryStat is { } attribute ? store.StatName(attribute) : null)),
            new Facet("hero", "Hero", e => e.Heroes ?? [])
            {
                Colour = (t, value) => t.Hero(Codename(store, value)),
            },
            new Facet("school", "School", Schools),
            new Facet("set", "Set", e => e.SetBonus ?? []),
            new Facet("resource", "Resource", e => One(e.Resource)),
            new Facet("gem", "Gem type", e => One(e.GemType))
            {
                Colour = (t, value) => t.Gem(value),
            },
            new Facet("trait", "Trait category", e => One(e.TraitCategory)),
            new Facet("role", "Role", e => e.AllowedRoles ?? [])
            {
                Colour = (t, value) => t.Role(value),
            },
            new Facet("rarity", "Rarity", e => One(e.Rarity))
            {
                Colour = (t, value) => t.Rarity(Filed(store, value)),
            },
        ];

        Flags =
        [
            new Flag("named", "Named in the game", e => PublicTypes.DisplayNameOf(e) is not null),
            new Flag("icon", "Has art", e => e.Icon is not null),
            new Flag("described", "Has description text", e => e.Description is not null),
            new Flag("grants", "Grants an ability", e => e.Grants is not null),
            new Flag("socket", "Rolls with a gem socket", e => e.GemSlot == true),
            new Flag("unique", "Unique equipped", e => e.UniqueEquipped == true),
            new Flag("scaling", "Scales off an attribute", e => Scaling(e.ScalesOff).Any()),
        ];

        Measures =
        [
            new Measure("cooldown", "Cooldown", e => e.Cooldown) { Unit = "s", Column = true },
            new Measure("cast", "Cast time", e => e.CastTime) { Unit = "s", Column = true },
            new Measure("channel", "Channel", e => e.ChannelTime) { Unit = "s" },
            new Measure("range", "Range", e => e.Range) { Column = true },
            new Measure("radius", "Radius", e => e.Radius),
            new Measure("duration", "Duration", e => e.Duration) { Unit = "s" },
            new Measure("cost", "Cost", e => e.Cost),
            new Measure("charges", "Charges", e => e.Charges),
            new Measure("tier", "Gem tier", e => e.GemTier),
            new Measure("rank", "Max rank", e => e.MaxRank),
            new Measure("id", "ID", e => e.NetId) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(e => e.Table!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Types =
        [
            .. PublicTypes.All
                .Select(type => new EntityType(
                    type.Table, type.Slug, type.Label, type.Group, counts.GetValueOrDefault(type.Table)))
                .Where(type => type.Count > 0),
        ];

        var reachable = store.Entities.Where(entity => PublicTypes.IsPublic(entity.Table)).ToArray();

        offered = Facets.ToDictionary(
            facet => facet.Key,
            facet => View(facet, reachable).Values.Select(value => value.Value).ToArray(),
            StringComparer.OrdinalIgnoreCase);

        Menu = BuildMenu();
    }

    public IReadOnlyList<Entity> Of(string table) =>
        [.. store.Entities.Where(e => string.Equals(e.Table, table, StringComparison.Ordinal))];

    public FacetView View(Facet facet, IReadOnlyList<Entity> rows)
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

    public string Printed(Facet facet, string value) =>
        facet.Display?.Invoke(store, value) ?? Spaced(value);

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
                var rows = Of(type.Table);
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

    public static IEnumerable<string> Schools(Entity entity) =>
        (entity.Tags ?? [])
            .Where(tag =>
                tag.StartsWith("Magic", StringComparison.Ordinal) ||
                tag.StartsWith("Physical", StringComparison.Ordinal))
            .Select(Spaced);

    public IEnumerable<string> Scaling(IReadOnlyList<string>? attributes) =>
        (attributes ?? []).Select(store.StatName).Distinct(StringComparer.Ordinal);

    private static string Codename(EntityStore store, string name) =>
        store.Hero(name)?.Codename ?? name;

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

    public static string Label(string? table) => PublicTypes.Label(table);

    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);
}
