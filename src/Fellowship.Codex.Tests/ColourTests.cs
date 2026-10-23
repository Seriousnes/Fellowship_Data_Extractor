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
    public void A_gem_type_and_its_augmentations_are_drawn_in_the_stones_colour()
    {
        var type = export.Store.Entities.OfType<RadianceDocument>().First();
        var augmentation = export.Store.Entities.OfType<GemAbilityDocument>().First(t => t.Gem == type.Type);
        var stone = CodexView.Readable(export.View.Tokens.Gem(type.Type.ToString()));

        Assert.Equal(stone, export.View.NameColour(type));
        Assert.Equal(stone, export.View.NameColour(augmentation));
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
