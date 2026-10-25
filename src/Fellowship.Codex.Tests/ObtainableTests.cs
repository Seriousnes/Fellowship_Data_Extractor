using Microsoft.Extensions.Logging.Abstractions;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class ObtainableTests(Export export)
{
    private const string UnobtainableFlag = "unobtainable";

    private readonly SearchIndexService index = new(export.Store, NullLogger<SearchIndexService>.Instance);

    private IReadOnlyList<ItemDocument> Items => [.. export.Store.Entities.OfType<ItemDocument>()];

    private static bool Unobtainable(ItemDocument item) =>
        item.EnabledInGame is false || PublicTypes.DisplayNameOf(item) is null;

    private IReadOnlyList<ItemDocument> Disabled =>
    [
        .. Items.Where(item => item.EnabledInGame is false && PublicTypes.DisplayNameOf(item) is not null),
    ];

    private CodexResults Run(IReadOnlyDictionary<string, bool> flags) =>
        new CodexSearchService(export.Store, export.View.Codex, index).Run(new CodexQuery
        {
            Types = [EntityType.Item],
            Flags = flags,
            PageSize = int.MaxValue,
        });

    [Fact]
    public void The_export_has_named_items_disabled_in_game() => Assert.NotEmpty(Disabled);

    [Fact]
    public void An_item_list_shows_only_obtainable_items_by_default()
    {
        var shown = Run(new Dictionary<string, bool>());

        Assert.Equal(Items.Count(item => !Unobtainable(item)), shown.Total);
        Assert.Contains(UnobtainableFlag, shown.Flagged);
    }

    [Fact]
    public void Showing_unobtainable_items_lists_every_item()
    {
        var shown = Run(new Dictionary<string, bool> { [UnobtainableFlag] = true });

        Assert.Equal(Items.Count, shown.Total);
    }

    [Fact]
    public void The_unobtainable_flag_set_to_no_is_the_default_list() =>
        Assert.Equal(
            Run(new Dictionary<string, bool>()).Total,
            Run(new Dictionary<string, bool> { [UnobtainableFlag] = false }).Total);

    [Fact]
    public void A_name_search_reaches_no_disabled_item()
    {
        var suggestions = new SuggestionService(index, export.View);
        var name = PublicTypes.DisplayNameOf(Disabled[0])!;

        Assert.DoesNotContain(
            suggestions.For(name, "item", SuggestionService.MaximumLimit).Results,
            hit => export.Store.All(EntityType.Item, hit.Id).All(item => item is ItemDocument { EnabledInGame: false }));
    }
}
