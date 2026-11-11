using Fellowship.SDK.Records;

namespace Fellowship.SDK.Tests;

[Collection(Corpus.Name)]
public sealed class ItemScalingTests(Checkout checkout)
{
    private static readonly EntityReference Orb = new(EntityType.Item, 5919, "Orb of Sanguine Restoration");

    private FellowshipExport Export => checkout.Export;

    [Fact]
    public void Orb_of_Sanguine_Restoration_at_315_tempered_8_of_8_has_27_expertise()
    {
        var scaling = new ItemScaling { Hero = "Aeona", ItemLevel = 315, Upgrades = 8 };

        var magnitudes = Export.Magnitudes(Orb, scaling);

        Assert.Equal(8, magnitudes.MaxUpgrades);
        Assert.Equal(27m, Assert.Single(magnitudes.Stats, stat => stat.Stat == "Expertise").Value);
    }

    [Fact]
    public void Each_temper_adds_one_to_a_secondary_row()
    {
        var untempered = Expertise(new ItemScaling { Hero = "Aeona", ItemLevel = 315 });
        var tempered = Expertise(new ItemScaling { Hero = "Aeona", ItemLevel = 315, Upgrades = 8 });

        Assert.Equal(untempered + 8m, tempered);
    }

    [Fact]
    public void A_higher_item_level_raises_a_row()
    {
        Assert.True(
            Expertise(new ItemScaling { Hero = "Aeona", ItemLevel = 400 }) >
            Expertise(new ItemScaling { Hero = "Aeona", ItemLevel = 200 }));
    }

    [Fact]
    public void An_item_with_no_item_level_reads_at_its_drop_item_level()
    {
        var item = Export.Find<ItemDocument>(Orb, "Aeona")!;

        Assert.Equal(Export.DropItemLevel(item), Export.Magnitudes(item, new ItemScaling()).ItemLevel);
    }

    [Fact]
    public void An_item_with_no_rarity_named_reads_at_its_highest()
    {
        var item = Export.Find<ItemDocument>(Orb, "Aeona")!;
        var highest = Export.Settings.Rarities.Single(rarity => rarity.Tier == (int)item.Rarities!.Max());

        Assert.Equal(highest.DisplayName, Export.Magnitudes(item, new ItemScaling()).Rarity);
    }

    [Fact]
    public void A_settings_table_matches_names_regardless_of_case()
    {
        var slot = Export.Settings.StatCalculation.SlotWeights.Keys.First();

        Assert.True(Export.Settings.StatCalculation.SlotWeights.ContainsKey(slot.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("Nobody", null, 315, 8, "No such hero.")]
    [InlineData("Aeona", "Bogus", 315, 8, "No such rarity.")]
    [InlineData("Aeona", null, 0, 8, "No such item level.")]
    [InlineData("Aeona", null, 315, -1, "No such tempering.")]
    [InlineData(null, null, 315, 8, "This id reads differently per hero.")]
    public void A_reading_the_codex_api_refuses_is_refused(
        string? hero, string? rarity, int itemLevel, int upgrades, string title)
    {
        var scaling = new ItemScaling { Hero = hero, Rarity = rarity, ItemLevel = itemLevel, Upgrades = upgrades };

        var refused = Assert.Throws<RefusedReadingException>(() => Export.Magnitudes(Orb, scaling));

        Assert.Equal(title, refused.Refusal.Title);
    }

    [Fact]
    public void A_named_tempering_ceiling_is_the_records_ceiling()
    {
        var magnitudes = Export.Magnitudes(Orb, new ItemScaling { Hero = "Aeona", ItemLevel = 315, MaxUpgrades = 10 });

        Assert.Equal(10, magnitudes.MaxUpgrades);
    }

    private decimal Expertise(ItemScaling scaling) =>
        Export.Stats(Orb, scaling).Single(line => line.Stat == "Expertise").Value;
}
