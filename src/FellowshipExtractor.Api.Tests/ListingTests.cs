using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class ListingTests(Export export)
{
    private const int Sinbinding = 5250;
    private const int Betrayers = 5191;

    private CodexResults Run(params (string Key, string Value)[] asked)
    {
        var query = CodexQuery.Parse(
            new QueryCollection(asked.ToDictionary(
                pair => pair.Key,
                pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value))),
            export.View.Codex,
            out var error);

        Assert.Null(error);

        return new CodexSearch(
            export.Store,
            export.View.Codex,
            new SearchIndex(export.Store, NullLogger<SearchIndex>.Instance),
            export.View).Run(query with { PageSize = 200 });
    }

    private ItemDocument Legendary => export.Store.Entities.OfType<ItemDocument>()
        .First(item => item.Rarity is { } rung && (int)rung == 6
            && export.Store.All(EntityType.Item, item.Id).Count > 1);

    private static Listing Find(CodexResults results, int id) =>
        results.Rows.Single(row => row.Shown.Id == id);

    [Fact]
    public void An_id_is_drawn_once_however_many_readings_it_has()
    {
        var results = Run(("type", "item"));
        var ids = results.Rows.Select(row => row.Shown.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(3789, results.Total);
    }

    [Fact]
    public void A_talent_slot_is_drawn_once_per_id()
    {
        Assert.Equal(19, Run(("type", "talent-slot")).Total);
    }

    [Fact]
    public void An_id_reading_under_one_name_draws_no_variant_link()
    {
        Assert.Empty(Find(Run(("type", "item"), ("q", "Sinbinding")), Sinbinding).Variants);
    }

    [Fact]
    public void An_id_reading_under_four_names_draws_the_other_three()
    {
        var row = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers);

        Assert.Equal(3, row.Variants.Count);
        Assert.DoesNotContain(export.View.Name(row.Shown), row.Variants.Select(variant => variant.Name));
        Assert.Equal(row.Variants.Count, row.Variants.Select(variant => variant.Name).Distinct().Count());
    }

    [Fact]
    public void A_filter_decides_which_reading_a_row_is_named_by()
    {
        var row = Find(Run(("type", "item"), ("hero", "Meiko")), Betrayers);

        Assert.Equal("Betrayer's Dragonhide Gloves", export.View.Name(row.Shown));
    }

    [Fact]
    public void A_variant_link_opens_the_reading_whose_name_it_shows()
    {
        var row = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers);

        Assert.All(row.Variants, variant =>
        {
            Assert.StartsWith($"/entity/item/{Betrayers}?hero=", variant.Href, StringComparison.Ordinal);

            var hero = Uri.UnescapeDataString(variant.Href.Split("?hero=")[1]);
            var reading = export.Store.All(EntityType.Item, Betrayers)
                .Single(read => export.Store.Wearer(read)?.Name == hero);

            Assert.Equal(export.View.Name(reading), variant.Name);
        });
    }

    [Fact]
    public void A_name_one_hero_reads_carries_that_heros_colour()
    {
        var legendary = Legendary;
        var row = Find(Run(("type", "item"), ("q", legendary.Name!)), legendary.Id);

        Assert.NotEmpty(row.Variants);
        Assert.All(row.Variants, variant => Assert.NotNull(variant.Colour));
    }

    [Fact]
    public void A_name_several_heroes_read_carries_none()
    {
        var shared = Find(Run(("type", "item"), ("q", "Betrayer's")), Betrayers).Variants;

        Assert.All(shared, variant => Assert.Null(variant.Colour));
    }

    [Fact]
    public void Every_count_beneath_the_list_counts_ids()
    {
        var results = Run(("type", "item"), ("hero", "Meiko"));

        Assert.Equal(results.Total, results.Types[EntityType.Item]);
        Assert.Equal(results.AcrossTypes, results.Types.Values.Sum());

        var slot = results.Facets.Single(facet => facet.Key == "slot");
        var chosen = slot.Values[0];

        Assert.Equal(chosen.Count, Run(("type", "item"), ("hero", "Meiko"), ("slot", chosen.Value)).Total);
    }
}
