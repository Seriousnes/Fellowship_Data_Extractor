namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class LegendaryTests(Export export)
{
    [Fact]
    public void A_legendary_item_lists_each_untagged_stat_at_its_definition_position()
    {
        var items = export.Store.Entities
            .OfType<ItemDocument>()
            .Where(item => item.UntaggedLegendaryStats is { Count: > 0 })
            .ToArray();

        Assert.NotEmpty(items);

        Assert.All(items, item =>
        {
            var entries = TooltipView.Legendary(export.Tooltip.LegendaryStats(item), item);

            Assert.Equal((item.LegendaryStats?.Count ?? 0) + item.UntaggedLegendaryStats!.Count, entries.Count);

            Assert.All(item.UntaggedLegendaryStats!, untagged =>
                Assert.Same(untagged, entries[untagged.Position].Untagged));
        });
    }
}
