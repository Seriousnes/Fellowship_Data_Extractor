using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

using static FellowshipExtractor.Api.Models.Codex;

namespace FellowshipExtractor.Api.Services;

public interface ICodexService
{
    IReadOnlyList<Facet> Facets { get; }

    IReadOnlyList<Flag> Flags { get; }

    IReadOnlyList<Measure> Measures { get; }

    IReadOnlyList<TypeView> Types { get; }

    int Count { get; }

    IReadOnlyList<MenuGroup> Menu { get; }

    IReadOnlyList<EntityDocument> Of(EntityType type);

    FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows);

    string Printed(Facet facet, string value);

    IReadOnlyList<string> Offered(Facet facet);

    Facet? Facet(string key);

    Measure? Measure(string key);

    Flag? Flag(string key);

    IEnumerable<string> Scaling(IReadOnlyList<string>? attributes);
}
public sealed class CodexService : ICodexService
{
    private const int MenuValueLimit = 12;

    private const int MenuSectionLimit = 4;

    private readonly IEntityStoreService store;
    private readonly IDesignTokenService tokens;

    private readonly IReadOnlyDictionary<string, string[]> offered;

    public IReadOnlyList<Facet> Facets { get; }

    public IReadOnlyList<Flag> Flags { get; }

    public IReadOnlyList<Measure> Measures { get; }

    public IReadOnlyList<TypeView> Types { get; }

    public int Count => store.Ids;

    public IReadOnlyList<MenuGroup> Menu { get; }

    public CodexService(IEntityStoreService store, IDesignTokenService tokens)
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

            new Facet("school", "School", Codex.Schools),
            new Facet("set", "Set", document => document is ItemDocument { SetBonus: { } sets } ? sets : []),
            new Facet("resource", "Resource", document =>
                One(document is AbilityDocument { Resource: { } resource } ? resource : null)),

            new Facet("gem", "Gem type", document => One(document switch
            {
                GemTypeDocument gem => gem.Type.ToString(),
                GemDocument gem => gem.Type.ToString(),
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
                GemDocument stone => RarityWord(store, stone.Rarity, gem: true),
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
            new Measure("rank", "Max rank", document => DocumentFields.Number(document, nameof(TraitDocument.MaxRank))),

            new Measure("id", "ID", document => (double?)document.Id) { Column = true },
        ];

        var counts = store.Entities
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Id).Distinct().Count());

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
        var counted = new Dictionary<string, HashSet<(EntityType, int)>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = (EntityTypes.TypeOf(row), row.Id);

            foreach (var value in facet.Values(row))
            {
                if (value.Length == 0) continue;
                if (!counted.TryGetValue(value, out var ids)) counted[value] = ids = [];

                ids.Add(key);
            }
        }

        return new FacetView(facet.Key, facet.Label,
        [
            .. counted
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(spellings => (
                    Value: Written(spellings),
                    Count: spellings.SelectMany(pair => pair.Value).Distinct().Count()))
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new FacetValue(
                    value.Value,
                    Printed(facet, value.Value),
                    value.Count,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, value.Value)))),
        ]);
    }

    private static string Written(IEnumerable<KeyValuePair<string, HashSet<(EntityType, int)>>> spellings) =>
        spellings
            .OrderByDescending(pair => pair.Value.Count)
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

    public IEnumerable<string> Scaling(IReadOnlyList<string>? attributes) =>
        (attributes ?? []).Select(store.StatName).Distinct(StringComparer.Ordinal);

    private static string Codename(IEntityStoreService store, string name) =>
        store.Hero(name)?.Codename ?? name;

    private static string RarityWord(IEntityStoreService store, Rarity rarity, bool gem) =>
        store.Settings.Rarities.FirstOrDefault(r => r.Tier == (int)rarity) is { } entry
            ? (gem ? entry.GemDisplayName : entry.DisplayName)
            : rarity.ToString();

    private static string Filed(IEntityStoreService store, string printed) =>
        store.Rarity(printed, gem: true)?.Name ?? printed;

}
