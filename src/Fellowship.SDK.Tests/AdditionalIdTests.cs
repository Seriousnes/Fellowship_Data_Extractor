using Fellowship.SDK.Documents;

namespace Fellowship.SDK.Tests;

[Collection(Corpus.Name)]
public sealed class AdditionalIdTests(Checkout checkout)
{
    private FellowshipExport Export => checkout.Export;

    [Theory]
    [InlineData(2308, 1129, "Mara_backstab")]
    [InlineData(2309, 1213, "Mara_AOEBuilder")]
    [InlineData(2227, 1018, "T_Rime_OnTargetPulsatingAOE")]
    public void An_additional_id_reads_the_ability_listing_it(int additional, int listing, string icon)
    {
        var outcome = Export.Resolve(EntityType.Ability, additional, null, out var document, out _);

        Assert.Equal(Lookup.Found, outcome);

        var ability = Assert.IsType<AbilityDocument>(document);

        Assert.Equal(listing, ability.Id);
        Assert.Equal(icon, ability.Icon);
    }
}
