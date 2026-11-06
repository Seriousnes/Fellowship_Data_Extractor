using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class ItemPoolTests(Export export)
{
    private IReadOnlyList<ItemDocument> Sourced =>
        [.. export.Store.Entities.OfType<ItemDocument>().Where(item => item.Sources is { Count: > 0 })];

    private IReadOnlyList<ItemDocument> Pool(ItemSource source, string? hero = null) =>
        ItemPools.Of(export.View.Codex, export.Store, source, hero);

    private ItemSource Of(SourceKind kind) =>
        Sourced.SelectMany(item => item.Sources!).First(source => source.Kind == kind && source.Dungeon is not null);

    [Fact]
    public void A_pool_lists_each_item_id_once()
    {
        var pool = Pool(Of(SourceKind.Dungeon));

        Assert.NotEmpty(pool);
        Assert.Equal(pool.Count, pool.Select(item => item.Id).Distinct().Count());
    }

    [Fact]
    public void A_pool_has_every_obtainable_item_from_that_source()
    {
        var source = Of(SourceKind.Dungeon);

        var obtainable = Sourced
            .Where(item => item.Sources!.Contains(source) && export.View.Codex.Flags.All(flag => flag.Admits(item)))
            .Select(item => item.Id)
            .ToHashSet();

        Assert.Equal(obtainable, Pool(source).Select(item => item.Id).ToHashSet());
    }

    [Fact]
    public void A_dungeon_pool_leaves_out_items_from_any_dungeon()
    {
        var any = Sourced
            .SelectMany(item => item.Sources!)
            .First(source => source.Kind == SourceKind.Dungeon && source.Dungeon is null);

        var general = Pool(any).Select(item => item.Id).ToHashSet();

        Assert.NotEmpty(general);
        Assert.DoesNotContain(Pool(Of(SourceKind.Dungeon)), item => general.Contains(item.Id));
    }

    [Fact]
    public void A_pool_for_a_hero_has_that_hero_reading_of_a_shared_item()
    {
        var source = Of(SourceKind.Dungeon);
        var shared = Sourced.First(item =>
            item.Sources!.Contains(source) && export.Store.All(EntityType.Item, item.Id).Count > 1);
        var hero = export.Store.Settings.Heroes.Last(known =>
            export.Store.All(EntityType.Item, shared.Id).Any(item => EntityStore.Reads(item, known)));

        var reading = Assert.Single(Pool(source, hero.Name), item => item.Id == shared.Id);

        Assert.True(EntityStore.Reads(reading, hero));
    }
}
