namespace FellowshipExtractor.Api.Tests;

[Collection(Corpus.Name)]
public class CodexTests
{
    private readonly Codex Codex;
    private readonly IEntityStoreService Store;

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
    public void The_rank_measure_still_reads_its_own_int_field()
    {
        var rank = Codex.Measure("rank");
        Assert.NotNull(rank);

        var trait = Store.Entities.OfType<TraitDocument>().First(t => t.MaxRank is not null);
        Assert.Equal((double)trait.MaxRank!.Value, rank!.Value(trait));
    }

    [Fact]
    public void A_gem_is_measured_on_no_ladder_beside_its_rung()
    {
        Assert.Null(Codex.Measure("tier"));

        Assert.DoesNotContain(
            typeof(GemDocument).GetProperties(),
            property => property.Name is "Tier");
    }
}
