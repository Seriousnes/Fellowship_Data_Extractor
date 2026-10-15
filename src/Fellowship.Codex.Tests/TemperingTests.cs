namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class TemperingTests(Export export)
{
    private int? Ceiling(string? rarity, int? itemLevel) =>
        Tempering.Ceiling(export.Store.Settings.MaxTempers, rarity, itemLevel);

    private static int DefaultItemLevel(Export export) =>
        export.Store.DropItemLevel(new ItemDocument { Id = 1 });

    [Fact]
    public void A_rare_item_at_the_level_it_drops_at_tempers_five_times()
    {
        Assert.Equal(5, Ceiling("Rare", DefaultItemLevel(export)));
    }

    [Fact]
    public void A_regal_item_at_item_level_285_tempers_four_times()
    {
        Assert.Equal(4, Ceiling("Heroic", 285));
        Assert.Equal(8, Ceiling("Heroic", DefaultItemLevel(export)));
    }

    [Fact]
    public void The_ceiling_steps_rather_than_interpolating()
    {
        Assert.Equal(1, Ceiling("Uncommon", 15));
        Assert.Equal(1, Ceiling("Uncommon", 60));
        Assert.Equal(2, Ceiling("Uncommon", 105));
    }

    [Fact]
    public void A_rarity_that_takes_no_tempering_answers_zero_at_every_item_level()
    {
        foreach (var rarity in new[] { "Common", "Legendary" })
        {
            foreach (var level in new[] { 0, 15, 105, 180, 255, 315, 420, 1000 })
            {
                Assert.Equal(0, Ceiling(rarity, level));
            }
        }
    }

    [Fact]
    public void An_item_level_below_the_first_key_holds_that_key()
    {
        Assert.Equal(0, Ceiling("Rare", 0));
        Assert.Equal(0, Ceiling("Rare", -50));
    }

    [Fact]
    public void An_unnamed_rarity_or_item_level_answers_nothing()
    {
        Assert.Null(Ceiling(null, DefaultItemLevel(export)));
        Assert.Null(Ceiling("Rare", null));
        Assert.Null(Ceiling("Nonesuch", DefaultItemLevel(export)));
    }
}
