using Fellowship.SDK.Documents;

namespace Fellowship.SDK.Tests;

[Collection(Corpus.Name)]
public sealed class AdditionalIdTests(Checkout checkout)
{
    private FellowshipExport Export => checkout.Export;

    [Theory]
    [InlineData(2308, 1129, AbilityCategory.Core)]
    [InlineData(2309, 1213, AbilityCategory.Core)]
    public void An_additional_id_reads_its_own_record_replacing_the_listing_ability(
        int additional, int listing, AbilityCategory category)
    {
        var outcome = Export.Resolve(EntityType.Ability, additional, null, out var document, out _);

        Assert.Equal(Lookup.Found, outcome);

        var ability = Assert.IsType<AbilityDocument>(document);

        Assert.Equal(additional, ability.Id);
        Assert.Equal(category, ability.Category);
        Assert.Equal(listing, ability.Replaces?.Id);
    }

    [Fact]
    public void An_additional_id_reads_the_utility_ability_its_listing_ability_subcasts()
    {
        Export.Resolve(EntityType.Ability, 2227, null, out var document, out _);

        var ability = Assert.IsType<AbilityDocument>(document);

        Assert.Equal(1018, ability.SubcastOf?.Id);
    }
}
