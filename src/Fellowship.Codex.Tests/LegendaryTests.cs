using Fellowship.SDK.Models;

using Microsoft.Extensions.Logging.Abstractions;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class LegendaryTests(Export export)
{
    [Fact]
    public void A_legendary_item_lists_each_untagged_stat_at_its_definition_position()
    {
        var rolls = export.Store.Entities.OfType<LegendaryStatDocument>().Take(2).ToArray();
        var untagged = new LegendaryStatText(1, "Untagged") { Description = "+10% Maximum Health" };
        var item = new ItemDocument { Id = 1, Name = "Legendary", UntaggedLegendaryStats = [untagged] };

        var entries = TooltipView.Legendary(rolls, item);

        Assert.Equal(3, entries.Count);
        Assert.Same(rolls[0], entries[0].Roll);
        Assert.Same(untagged, entries[1].Untagged);
        Assert.Same(rolls[1], entries[2].Roll);
    }

    [Fact]
    public void A_legendary_item_lists_each_stat_it_names()
    {
        var items = export.Store.Entities.OfType<ItemDocument>().Where(item => item.LegendaryStats is { Count: > 0 }).ToArray();

        Assert.NotEmpty(items);

        Assert.All(items, item => Assert.Equal(
            item.LegendaryStats!.Count + (item.UntaggedLegendaryStats?.Count ?? 0),
            TooltipView.Legendary(export.Tooltip.LegendaryStats(item), item).Count));
    }

    [Fact]
    public void A_legendary_item_is_found_by_the_name_of_the_power_it_grants()
    {
        var index = new SearchIndexService(export.Store, NullLogger<SearchIndexService>.Instance);
        var items = export.Store.Entities
            .OfType<ItemDocument>()
            .Where(item => item.Grants?.Name is not null && PublicTypes.DisplayNameOf(item) is not null)
            .ToArray();

        Assert.NotEmpty(items);

        Assert.All(items, item => Assert.Contains(
            index.Search(item.Grants!.Name!, int.MaxValue),
            hit => hit.Document is ItemDocument found && found.Id == item.Id));
    }
}