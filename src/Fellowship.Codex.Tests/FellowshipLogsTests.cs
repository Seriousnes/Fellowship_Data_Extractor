namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class FellowshipLogsTests(Export export)
{
    [Fact]
    public void A_blessing_resolves_through_the_per_hero_ability_rank_id()
    {
        var blessing = export.Store.Entities
            .OfType<BlessingDocument>()
            .First(row => row.AbilityRanks is { Count: > 0 });

        var (_, rankId) = blessing.AbilityRanks!.First();

        Assert.Equal(
            blessing.Id,
            FellowshipLogs.Blessing(rankId + FellowshipLogs.BlessingBase, export.Store));
    }

    [Fact]
    public void The_attribute_table_names_the_nine_an_item_can_carry()
    {
        Assert.Equal(9, FellowshipLogs.Attributes.Count);

        foreach (var (_, stat) in FellowshipLogs.Attributes)
        {
            Assert.NotNull(export.Store.StatDisplay(stat));
            Assert.Contains(stat, export.Store.Settings.StatCalculation.Attributes.Keys, StringComparer.Ordinal);
        }
    }
}
