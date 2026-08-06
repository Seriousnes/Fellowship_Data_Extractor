using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Tests;

/// <summary>
/// The <see cref="Codex"/> measures read off the real corpus, on the terms
/// <see cref="DocumentFields.Number"/> reads a document's own scalar field: every numeric property a
/// measure names, whatever its declared type, rather than only the ones already <c>double</c>.
/// </summary>
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

    /// <summary>
    /// <see cref="TalentSlot.Cost"/> is a required <c>int</c>, the one <c>Cost</c> among
    /// <see cref="Ability"/>'s, <see cref="Effect"/>'s, <see cref="Talent"/>'s and
    /// <see cref="TalentSlot"/>'s four that is not a <c>double</c>. <c>DocumentFields.Number</c> used
    /// to read every <c>Cost</c> through <c>Property(...)?.GetValue(document) as double?</c>, which
    /// only ever unboxes a boxed <c>double</c>, so a talent slot's own boxed <c>int</c> silently
    /// answered null: the cost measure dropped every one of the corpus's 216 talent slots from the
    /// cost filter, the cost sort and the cost column, even though <see cref="CodexView.Facts"/>
    /// still printed a cost on the entity page through its own <c>int number =></c> arm. Fails against
    /// the old <c>as double?</c>, which answered null for every row asserted here.
    /// </summary>
    [Fact]
    public void The_cost_measure_reads_every_talent_slots_own_int_cost()
    {
        var measure = Codex.Measure("cost");
        Assert.NotNull(measure);

        var slots = Store.Entities.OfType<TalentSlot>().ToArray();
        Assert.Equal(216, slots.Length);

        Assert.All(slots, slot => Assert.Equal((double)slot.Cost, measure!.Value(slot)));
    }

    /// <summary>
    /// The two measures that already worked around the same restriction with an explicit cast -
    /// <see cref="GemTier.Tier"/>, a required <c>int</c>, and <see cref="Trait.MaxRank"/>, an
    /// <c>int?</c> - still answer once they are simplified onto the same general path the cost measure
    /// now takes, so the general fix rather than a cost-only patch is what is under test.
    /// </summary>
    [Fact]
    public void The_tier_and_rank_measures_still_read_their_own_int_fields()
    {
        var tier = Codex.Measure("tier");
        var rank = Codex.Measure("rank");
        Assert.NotNull(tier);
        Assert.NotNull(rank);

        var gemTier = Store.Entities.OfType<GemTier>().First();
        Assert.Equal((double)gemTier.Tier, tier!.Value(gemTier));

        var trait = Store.Entities.OfType<Trait>().First(t => t.MaxRank is not null);
        Assert.Equal((double)trait.MaxRank!.Value, rank!.Value(trait));
    }
}
