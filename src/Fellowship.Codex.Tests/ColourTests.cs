namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ColourTests(Export export)
{
    private string? Shipped(int tier) =>
        CodexView.Readable(export.View.Tokens.Rarity(
            export.Store.Settings.Rarities.Single(rung => rung.Tier == tier).Name));

    [Fact]
    public void A_dropped_item_is_drawn_at_the_top_of_its_own_ladder()
    {
        var dropped = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Rarities is { Count: > 1 });

        Assert.Equal(Shipped((int)dropped.Rarities![^1]), export.View.NameColour(dropped));
    }

    [Fact]
    public void A_legendary_item_is_drawn_at_the_legendary_rung()
    {
        var legendary = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Rarity is { } rung && (int)rung == 6);

        Assert.Equal(Shipped(6), export.View.NameColour(legendary));
    }

    [Fact]
    public void A_gem_tier_is_drawn_at_the_rung_it_states()
    {
        var tier = export.Store.Entities.OfType<GemDocument>().First();

        Assert.Equal(Shipped((int)tier.Rarity), export.View.NameColour(tier));
    }

    [Fact]
    public void A_gem_type_and_its_traits_are_drawn_in_the_stones_colour()
    {
        var type = export.Store.Entities.OfType<GemTypeDocument>().First();
        var trait = export.Store.Entities.OfType<GemTraitDocument>().First(t => t.Gem == type.Type);
        var stone = CodexView.Readable(export.View.Tokens.Gem(type.Type.ToString()));

        Assert.Equal(stone, export.View.NameColour(type));
        Assert.Equal(stone, export.View.NameColour(trait));
    }

    [Fact]
    public void An_item_carrying_one_hero_is_not_drawn_in_that_heros_colour()
    {
        var worn = export.Store.Entities.OfType<ItemDocument>()
            .First(item => item.Heroes is { Count: 1 });

        Assert.NotEqual(export.View.HeroColour(worn.Heroes![0].ToString()), export.View.NameColour(worn));
    }

    [Fact]
    public void A_talent_slot_is_not_drawn_in_its_heros_colour()
    {
        var slot = export.Store.Entities.OfType<TalentSlotDocument>().First();

        Assert.NotEqual(export.View.HeroColour(slot.Hero.ToString()), export.View.NameColour(slot));
    }

    [Fact]
    public void A_row_the_build_gives_no_colour_asks_for_none()
    {
        var ability = export.Store.Entities.OfType<AbilityDocument>()
            .First(named => PublicTypes.DisplayNameOf(named) is not null);

        Assert.Null(export.View.LinkColour(ability));
        Assert.Equal(export.View.Tokens.Color("core.text-primary"), export.View.NameColour(ability));
    }

    [Fact]
    public void A_row_with_no_name_is_drawn_in_the_secondary_text_colour()
    {
        var nameless = export.Store.Entities.OfType<TalentSlotDocument>()
            .First(slot => PublicTypes.DisplayNameOf(slot) is null);

        Assert.Equal(export.View.Tokens.Color("core.text-secondary"), export.View.NameColour(nameless));
    }
}
