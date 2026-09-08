using Fellowship.SDK.Client;

namespace Fellowship.Codex.Tests;

public class CodexAddressTests
{
    [Fact]
    public void A_tooltip_is_addressed_by_its_type_and_id_alone()
    {
        Assert.Equal("api/item/5218/tooltip", CodexAddresses.Tooltip(EntityType.Item, 5218));
    }

    [Fact]
    public void A_modifier_and_a_trait_are_written_as_the_tooltip_reads_them_back()
    {
        Assert.True(ItemModifiers.TryParse("blessing:12:3", out var modifiers));
        Assert.Equal("blessing:12:3", modifiers.Values.Single().ToString());

        Assert.True(ImbuedTraits.TryParse("456:2", out var traits));
        Assert.Equal("456:2", traits.Values.Single().ToString());
    }

    [Fact]
    public void A_page_and_a_record_address_the_same_entity()
    {
        Assert.Equal("item/5218?hero=Gunde", CodexAddresses.Page(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("api/item/5218?hero=Gunde", CodexAddresses.Record(EntityType.Item, 5218, "Gunde"));
        Assert.Equal("ability/1964", CodexAddresses.Page(EntityType.Ability, 1964));
    }
}
