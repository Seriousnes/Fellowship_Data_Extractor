using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface IMenuService
{
    IReadOnlyList<MenuLink> Menu { get; }

    IReadOnlyList<string> Roster { get; }
}

public sealed class MenuService : IMenuService
{
    private readonly IEntityStoreService store;
    private readonly ICodexService codex;
    private readonly IDesignTokenService tokens;

    public IReadOnlyList<string> Roster { get; }

    public IReadOnlyList<MenuLink> Menu { get; }

    public MenuService(IEntityStoreService store, ICodexService codex, IDesignTokenService tokens)
    {
        this.store = store;
        this.codex = codex;
        this.tokens = tokens;

        Roster =
        [
            .. store.Settings.Heroes
                .Select(who => who.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase),
        ];

        Menu =
        [
            Category(Categories.Spells, new MenuSection(null,
            [
                Tab(EntityType.Ability),
                Tab(EntityType.Effect),
                Tab(EntityType.Talent, new MenuSection("Hero", Heroes(Where(EntityType.Talent)))),
                Tab(EntityType.Trait),
                Tab(EntityType.GemTrait),
                Tab(EntityType.SetBonus),
                Tab(EntityType.SetBonusPower),
                Tab(EntityType.TraitPower),
                Tab(EntityType.WeaponPower),
                Tab(EntityType.RelicPower),
                Tab(EntityType.LegendaryPower),
            ])),

            Category(Categories.Items, new MenuSection(null,
            [
                Tab(EntityType.Item,
                    new MenuSection("Armour", Armour(Where(EntityType.Item))),
                    new MenuSection("Accessories", Accessories(Where(EntityType.Item))),
                    new MenuSection("Weapons", Weapons(Where(EntityType.Item)))),
                Tab(EntityType.Blessing),
                Tab(EntityType.Gem, new MenuSection("Types", Stones(Where(EntityType.Gem)))),
                Tab(EntityType.Currency),
                Tab(EntityType.Mount),
                Tab(EntityType.CompanionPet),
            ])),

            Category(Categories.Dungeons),

            Category(Categories.Npcs, new MenuSection("Zone", Zones($"/{Categories.Npcs.Slug}"))),
        ];
    }

    private IReadOnlyList<MenuLink> Armour(string where) =>
    [
        .. Enum.GetValues<ArmorType>()
            .Select(worn => Narrow(where, "kind", worn.ToString(), null,
                new MenuSection("Slot", Worn($"{where}&kind={worn}")))),
    ];

    private static IReadOnlyList<MenuLink> Accessories(string where) =>
        [.. CodexService.SlotWords.Select(slot => Narrow(where, "kind", slot, null))];

    private IReadOnlyList<MenuLink> Weapons(string where) =>
        [.. Roster.Select(who => Narrow(where, "kind", who, Colour(tokens.Hero(who))))];

    private IReadOnlyList<MenuLink> Heroes(string where) =>
        [.. Roster.Select(who => Narrow(where, "hero", who, Colour(tokens.Hero(who))))];

    private IReadOnlyList<MenuLink> Worn(string where) =>
    [
        .. store.Settings.StatCalculation.SlotWeights.Keys
            .Where(slot => !CodexService.SlotWords.Contains(slot, StringComparer.OrdinalIgnoreCase))
            .Where(slot => !string.Equals(slot, CodexService.WeaponSlot, StringComparison.OrdinalIgnoreCase))
            .Select(slot => Narrow(where, "slot", slot, null)),
    ];

    private IReadOnlyList<MenuLink> Stones(string where) =>
    [
        .. Enum.GetValues<GemType>()
            .Select(cut => Narrow(where, "gem", cut.ToString(), Colour(tokens.Gem(cut.ToString())))),
    ];

    private IReadOnlyList<MenuLink> Zones(string where) =>
        [.. Named("source", EntityType.Creature).Select(zone => Narrow(where, "source", zone, null))];

    private IEnumerable<string> Named(string key, EntityType type)
    {
        if (codex.Facet(key) is not { } facet)
        {
            throw new InvalidOperationException(
                $"The menu lists {PublicTypes.Label(type)} by '{key}', and that names no facet, so the rail could " +
                $"not filter by what the menu offers. The facets are " +
                $"{string.Join(", ", codex.Facets.Select(one => one.Key))}.");
        }

        return codex.View(facet, codex.Of(type)).Values
            .Select(value => value.Value)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
    }

    private static MenuLink Category(Category category, params MenuSection[] sections) =>
        new(category.Label, $"/{category.Slug}", sections);

    private static MenuLink Tab(EntityType type, params MenuSection[] sections) =>
        new(PublicTypes.Label(type), Where(type), sections);

    private static MenuLink Narrow(
        string where, string key, string value, string? colour, params MenuSection[] sections) =>
        new(FellowshipCodex.Spaced(value), $"{where}&{key}={Uri.EscapeDataString(value)}", colour, sections);

    private static string Where(EntityType type) => PublicTypes.Where(type);

    private static string? Colour(string? colour) => CodexView.Readable(colour);
}
