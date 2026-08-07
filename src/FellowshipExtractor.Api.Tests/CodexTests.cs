namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CodexTests
{
    private readonly Codex Codex;
    private readonly EntityStore Store;

    public CodexTests(Export export)
    {
        Codex = export.View.Codex;
        Store = export.Store;
    }

    [Fact]
    public void The_cost_measure_reads_every_talent_slots_own_int_cost()
    {
        var measure = Codex.Measure("cost");
        Assert.NotNull(measure);

        var slots = Store.Entities.OfType<TalentSlotDocument>().ToArray();
        Assert.Equal(216, slots.Length);

        Assert.All(slots, slot => Assert.Equal((double)slot.Cost, measure!.Value(slot)));
    }

    [Fact]
    public void The_tier_and_rank_measures_still_read_their_own_int_fields()
    {
        var tier = Codex.Measure("tier");
        var rank = Codex.Measure("rank");
        Assert.NotNull(tier);
        Assert.NotNull(rank);

        var gemTier = Store.Entities.OfType<GemDocument>().First();
        Assert.Equal((double)gemTier.Tier, tier!.Value(gemTier));

        var trait = Store.Entities.OfType<TraitDocument>().First(t => t.MaxRank is not null);
        Assert.Equal((double)trait.MaxRank!.Value, rank!.Value(trait));
    }
}
