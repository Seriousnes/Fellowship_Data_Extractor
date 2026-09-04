using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class MenuTests(Export export)
{
    private IReadOnlyList<MenuLink> Menu => export.Menu.Menu;

    private static IEnumerable<MenuLink> Every(IEnumerable<MenuLink> links) =>
        links.SelectMany(link => Under(link).Prepend(link));

    private static IEnumerable<MenuLink> Under(MenuLink link) =>
        Every(link.Sections.SelectMany(section => section.Links));

    private MenuLink Row(Category category) =>
        Menu.Single(link => link.Href == $"/{category.Slug}");

    private MenuLink Row(EntityType type) =>
        Every(Menu).Single(link => link.Href == PublicTypes.Where(type));

    private static MenuSection Section(MenuLink link, string head) =>
        link.Sections.Single(section => section.Head == head);

    [Fact]
    public void Every_link_the_menu_offers_answers_with_rows()
    {
        foreach (var link in Every(Menu))
        {
            var split = link.Href.Split('?');
            var category = Categories.Find(split[0].TrimStart('/'));

            Assert.True(category is not null, $"{link.Label} points at {link.Href}, which is no category.");

            var asked = new QueryCollection(QueryHelpers.ParseQuery(split.Length > 1 ? split[1] : string.Empty));

            var query = CodexQuery.Parse(
                asked, export.View.Codex, RequestProperties.Of(typeof(EntitiesRequest)), category, out var error);

            Assert.True(error is null, $"{link.Label} points at {link.Href}, which the rail reads as: {error}");
            Assert.True(Searching.Run(export, query).Total > 0, $"{link.Label} points at {link.Href}, which is empty.");
        }
    }

    [Fact]
    public void The_menu_opens_on_the_four_categories_in_the_order_they_are_declared()
    {
        Assert.Equal([.. Categories.All.Select(category => category.Label)], [.. Menu.Select(link => link.Label)]);
        Assert.Equal([.. Categories.All.Select(category => $"/{category.Slug}")], [.. Menu.Select(link => link.Href)]);
    }

    [Fact]
    public void Spells_and_items_list_their_types_under_no_heading()
    {
        foreach (var category in new[] { Categories.Spells, Categories.Items })
        {
            var section = Assert.Single(Row(category).Sections);

            Assert.Null(section.Head);
            Assert.NotEmpty(section.Links);
        }
    }

    [Fact]
    public void Abilities_and_effects_and_dungeons_are_plain_links()
    {
        Assert.Empty(Row(EntityType.Ability).Sections);
        Assert.Empty(Row(EntityType.Effect).Sections);
        Assert.Empty(Row(Categories.Dungeons).Sections);

        Assert.DoesNotContain(Every(Menu), link => link.Href == PublicTypes.Where(EntityType.Dungeon));
    }

    [Fact]
    public void Talents_fly_out_to_the_roster()
    {
        var heroes = Section(Row(EntityType.Talent), "Hero");

        Assert.Equal(export.Menu.Roster, [.. heroes.Links.Select(link => link.Label)]);

        foreach (var hero in heroes.Links)
        {
            Assert.Equal($"/spells?type=talent&hero={Uri.EscapeDataString(hero.Label)}", hero.Href);
            Assert.NotNull(hero.Colour);
        }
    }

    [Fact]
    public void Items_group_into_armour_and_accessories_and_weapons()
    {
        var items = Row(EntityType.Item);

        Assert.Equal<string?>(
            ["Armour", "Accessories", "Weapons"], [.. items.Sections.Select(section => section.Head)]);

        Assert.Equal(
            [.. Enum.GetValues<ArmorType>().Select(worn => worn.ToString())],
            [.. Section(items, "Armour").Links.Select(link => link.Label)]);

        Assert.Equal(
            [.. CodexService.SlotWords],
            [.. Section(items, "Accessories").Links.Select(link => link.Label)]);

        Assert.Equal(export.Menu.Roster, [.. Section(items, "Weapons").Links.Select(link => link.Label)]);
    }

    [Fact]
    public void An_armour_type_flies_out_to_the_slots_it_covers_and_no_accessory()
    {
        var plate = Section(Row(EntityType.Item), "Armour").Links.Single(link => link.Label == "Plate");

        var slots = Section(plate, "Slot").Links;

        Assert.Contains(slots, link => link.Label == "Chest");
        Assert.DoesNotContain(slots, link => CodexService.SlotWords.Contains(link.Label));
        Assert.DoesNotContain(slots, link => link.Label == CodexService.WeaponSlot);

        Assert.Equal(
            "/items?type=item&kind=Plate&slot=Chest",
            slots.Single(link => link.Label == "Chest").Href);
    }

    [Fact]
    public void Gems_fly_out_to_their_types()
    {
        var types = Section(Row(EntityType.Gem), "Types");

        Assert.Equal(
            [.. Enum.GetValues<GemType>().Select(cut => cut.ToString())],
            [.. types.Links.Select(link => link.Label)]);

        Assert.All(types.Links, link => Assert.NotNull(link.Colour));
    }

    [Fact]
    public void Npcs_fly_out_to_the_zones_they_are_found_in()
    {
        var zones = Section(Row(Categories.Npcs), "Zone").Links;

        Assert.NotEmpty(zones);
        Assert.Equal([.. zones.Select(zone => zone.Label).Order(StringComparer.OrdinalIgnoreCase)],
            [.. zones.Select(zone => zone.Label)]);

        Assert.All(zones, zone => Assert.StartsWith("/npcs?source=", zone.Href, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_hero_list_reads_the_roster_in_the_one_order()
    {
        var lists = Every(Menu)
            .SelectMany(link => link.Sections)
            .Where(section => section.Head is "Hero" or "Weapons")
            .Select(section => section.Links.Select(link => link.Label).ToArray())
            .ToArray();

        Assert.NotEmpty(lists);
        Assert.All(lists, list => Assert.Equal(export.Menu.Roster, list));
    }

    [Fact]
    public void The_roster_is_the_build_roster_sorted_by_name()
    {
        Assert.Equal(
            [.. export.Store.Settings.Heroes.Select(who => who.Name).Order(StringComparer.OrdinalIgnoreCase)],
            export.Menu.Roster);
    }

    [Fact]
    public void No_menu_link_carries_a_count_or_a_school()
    {
        Assert.All(Every(Menu), link =>
        {
            Assert.DoesNotContain("school", link.Href, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("School", link.Label, StringComparison.Ordinal);
        });

        Assert.All(
            Every(Menu).SelectMany(link => link.Sections),
            section => Assert.NotEqual("School", section.Head));
    }
}
