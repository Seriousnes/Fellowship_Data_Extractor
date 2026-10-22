namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class RollOrdinalTests(Export export)
{
    private string Rolling => export.Store.Settings.StatCalculation.RandomStatPool[0];

    private ItemDocument Declaring(string stat) => new()
    {
        Id = 1,
        Slot = "Head",
        RuleSet = Rolled,
        Stats = [new StatSlot(stat, 40) { Kind = SlotKinds.Primary }],
    };

    private RarityEntry Regal => export.Store.Settings.Rarities.Single(rung => rung.Tier == 5);

    private string Rolled => export.Store.Settings.StatCalculation.RarityAdditions
        .First(rule => rule.Value.TryGetValue(Regal.Tier, out var added) &&
            added.GuaranteedSlots.Contains(CraftSlots.RandomStat, StringComparer.OrdinalIgnoreCase))
        .Key;

    private IReadOnlyList<StatLine> Lines(ItemDocument item, params string[] rolled) =>
        export.Tooltip.Rolls(item, new TooltipQuery { ItemLevel = 360, RandomStats = rolled }, Regal);

    [Fact]
    public void A_roll_that_folds_into_a_declared_line_leaves_its_ordinal_on_that_line()
    {
        var stat = Rolling;

        var line = Lines(Declaring(stat), stat, stat)
            .Single(row => row.Stat is { } named && named.Equals(stat, StringComparison.OrdinalIgnoreCase));

        Assert.Equal([0, 1], line.Rolls);
    }

    [Fact]
    public void Every_roll_the_rarity_grants_is_named_once_across_the_lines_drawn()
    {
        var item = Declaring(Rolling);
        var slots = ItemCraft.Slots(item, export.Store.Settings, Regal)
            .Count(slot => slot.SlotName == CraftSlots.RandomStat);

        Assert.True(slots > 0);

        var named = Lines(item, Rolling).SelectMany(line => line.Rolls).Order().ToArray();

        Assert.Equal([.. Enumerable.Range(0, slots)], named);
    }

    [Fact]
    public void A_declared_line_no_roll_landed_on_names_none()
    {
        var stat = Rolling;
        var other = export.Store.Settings.StatCalculation.RandomStatPool[1];

        var declared = Lines(Declaring(stat), other)
            .Single(row => row.Stat is { } named && named.Equals(stat, StringComparison.OrdinalIgnoreCase));

        Assert.Empty(declared.Rolls);
    }
}
