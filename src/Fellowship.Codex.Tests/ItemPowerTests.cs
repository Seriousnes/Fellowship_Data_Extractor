namespace Fellowship.Codex.Tests;

[Collection(Readings25647984.Name)]
public sealed class ItemPowerTests(Export25647984 export)
{
    private SettingsFile Settings => export.Store.Settings;

    private static ItemPowerInputs Bare(string slot) =>
        new(slot, Level: 0, MaxUpgrades: 0, RerollableSlots: 0,
            Legendary: false, HasGemSocket: false, HasGrantedAbility: false,
            HasTraitTree: false, HasSetBonus: false);

    private float Of(ItemPowerInputs given) => ItemPower.Potential(given, Settings);

    private string Printed(string type) =>
        ItemPower.Printed(Settings.Items, type)
        ?? throw new InvalidOperationException($"The settings name no slot for {type}.");

    [Fact]
    public void TheSlotWeightsJoinByItemTypeSoTheRenamedSlotsKeepTheirOwnWeight()
    {
        Assert.Equal("Neck", Printed(ItemPower.NecklaceType));
        Assert.Equal("Relic", Printed(ItemPower.TrinketType));
        Assert.Equal("Weapon", Printed(ItemPower.WeaponType));

        Assert.Equal(0.09, Settings.StatCalculation.SlotWeights["Legs"].StatWeight, 6);
        Assert.Equal(0.06, Settings.StatCalculation.SlotWeights["Relic"].StatWeight, 6);
        Assert.Equal(0.11, Settings.StatCalculation.SlotWeights["Weapon"].StatWeight, 6);
    }

    [Fact]
    public void AGrantedAbilityScoresOnAWeaponAndARelicAndNowhereElse()
    {
        var factors = Settings.Items.PowerPotential;

        foreach (var slot in Settings.StatCalculation.SlotWeights.Keys)
        {
            var without = Of(Bare(slot));
            var with = Of(Bare(slot) with { HasGrantedAbility = true });

            var expected = slot == Printed(ItemPower.WeaponType)
                ? (float)factors.WeaponAbilityScoreFactor
                : slot == Printed(ItemPower.TrinketType)
                    ? (float)factors.RelicAbilityScoreFactor
                    : 0f;

            Assert.Equal(expected, with - without, 3);
        }
    }

    [Fact]
    public void ATraitTreeScoresOnAWeaponAndANeckAndNowhereElse()
    {
        var factors = Settings.Items.PowerPotential;

        foreach (var slot in Settings.StatCalculation.SlotWeights.Keys)
        {
            var without = Of(Bare(slot));
            var with = Of(Bare(slot) with { HasTraitTree = true });

            var expected = slot == Printed(ItemPower.WeaponType)
                ? (float)factors.WeaponTraitTreeScoreFactor
                : slot == Printed(ItemPower.NecklaceType)
                    ? (float)factors.NecklaceTraitTreeScoreFactor
                    : 0f;

            Assert.Equal(expected, with - without, 3);
        }
    }

    [Fact]
    public void AnItemWhoseSlotTheWeightsDoNotCoverScoresNothingRatherThanANumberMissingItsLevelTerm()
    {
        var unworn = new ItemDocument { Id = 1, Slot = "Sporran" };

        Assert.Null(ItemPower.Inputs(unworn, Settings, null, new TooltipQuery(), 300));
        Assert.Null(ItemPower.Of(unworn, Settings, null, new TooltipQuery(), 300));
    }

    [Fact]
    public void AnItemWithNoSlotOrNoLevelScoresNothing()
    {
        var slotless = new ItemDocument { Id = 2 };
        var worn = new ItemDocument { Id = 3, Slot = "Chest" };

        Assert.Null(ItemPower.Of(slotless, Settings, null, new TooltipQuery(), 300));
        Assert.Null(ItemPower.Of(worn, Settings, null, new TooltipQuery(), null));
    }

    [Fact]
    public void TheSlotsTheRungAddsAreReadFromTheSameTableTheCraftingViewReads()
    {
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Heroic");
        var weapon = new ItemDocument { Id = 5, Slot = "Weapon", RuleSet = "Weapon" };

        var given = ItemPower.Inputs(weapon, Settings, rung, new TooltipQuery(), 300);

        Assert.NotNull(given);
        Assert.True(given.HasGrantedAbility);
        Assert.True(given.HasTraitTree);
        Assert.False(given.HasGemSocket);
        Assert.Equal(2, given.RerollableSlots);
    }

    private string? Printed(int id, string rung, int upgrades, int? itemLevel = null)
    {
        var item = (ItemDocument)export.Store.All(EntityType.Item, id)[0];
        var query = new TooltipQuery { Rarity = rung, Upgrades = upgrades, ItemLevel = itemLevel };

        return export.Tooltip.Potential(
            item, query, export.Tooltip.Rarity(item, query), export.Tooltip.Level(item, query));
    }

    [Fact]
    public void AshasChronoshiftSpireReadsTheEightHundredAndTwentyFiveTheGamePrints() =>
        Assert.Equal("825", Printed(5326, "Epic", upgrades: 0));

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void TwoGlovesTemperedDifferentlyReadTheSameFourHundredAndEightyFive(int upgrades) =>
        Assert.Equal("485", Printed(GlovesId, "Regal", upgrades));

    [Fact]
    public void WitchslayersLegsReadTheSixHundredAndSeventeenTheGamePrints() =>
        Assert.Equal("617", Printed(5309, "Regal", upgrades: 8));

    [Fact]
    public void ASetPieceScoresTheSetBonusOnTopOfTheTwoRollsItsRungAdds()
    {
        var legs = (ItemDocument)export.Store.All(EntityType.Item, 5309)[0];
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Heroic");

        var given = ItemPower.Inputs(legs, Settings, rung, new TooltipQuery(), 315);

        Assert.NotNull(given);
        Assert.True(given.HasSetBonus);
        Assert.Equal(2, given.RerollableSlots);
        Assert.Equal(8, given.MaxUpgrades);

        Assert.Equal(
            (float)Settings.Items.PowerPotential.SetBonusScoreFactor,
            ItemPower.Potential(given, Settings)
            - ItemPower.Potential(given with { HasSetBonus = false }, Settings),
            3);
    }

    [Fact]
    public void GrimoireOfResurrectionReadsTheFourHundredAndEightyFiveTheGamePrints() =>
        Assert.Equal("485", Printed(5254, "Epic", upgrades: 0));

    [Fact]
    public void AncientPoultryFetishReadsTheFiveHundredAndFortyFiveTheGamePrints() =>
        Assert.Equal("545", Printed(5252, "Regal", upgrades: 0));

    [Fact]
    public void PendantOfTheDeepReadsTheFourHundredAndSeventyTheGamePrints() =>
        Assert.Equal("470", Printed(5235, "Heroic", upgrades: 0, itemLevel: 255));

    [Fact]
    public void ANecklaceScoresItsTraitTreeFlatSoAnUnallocatedTreeScoresTheSameAsAFullOne()
    {
        var pendant = (ItemDocument)export.Store.All(EntityType.Item, 5235)[0];
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Champion");

        var given = ItemPower.Inputs(pendant, Settings, rung, new TooltipQuery(), 255);

        Assert.NotNull(given);
        Assert.True(given.HasTraitTree);

        var allocated = ItemPower.Inputs(
            pendant, Settings, rung, new TooltipQuery { AllocatedTraits = 2 }, 255);

        Assert.NotNull(allocated);
        Assert.Equal(ItemPower.Potential(given, Settings), ItemPower.Potential(allocated, Settings));
    }

    [Fact]
    public void MasochisticRazorLoopReadsTheOneThousandThreeHundredAndFortyTwoTheGamePrints() =>
        Assert.Equal("1342", Printed(5221, "Legendary", upgrades: 0));

    [Fact]
    public void TheAbilityAndTraitTreeARingCarriesScoreNothingBecauseARingIsNeitherGateSlot()
    {
        var ring = (ItemDocument)export.Store.All(EntityType.Item, 5221)[0];
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Legendary");

        var given = ItemPower.Inputs(ring, Settings, rung, new TooltipQuery(), 360);

        Assert.NotNull(given);
        Assert.True(given.HasGrantedAbility);
        Assert.True(given.HasGemSocket);
        Assert.Equal(0, given.MaxUpgrades);
        Assert.Equal(0, given.RerollableSlots);

        var factors = Settings.Items.PowerPotential;
        var scored = ItemPower.Potential(given, Settings);
        var ungated = ItemPower.Potential(given with { HasGrantedAbility = false }, Settings);

        Assert.Equal(ungated, scored);
        Assert.Equal(
            360 * (float)factors.ItemLevelScoreFactor * 0.06f
            + (float)factors.LegendaryScoreFactor
            + (float)factors.GemSocketScoreFactor,
            scored,
            3);
    }

    private int GlovesId => export.Store.Entities
        .OfType<ItemDocument>()
        .First(item => item.Name == "Webbed Gloves")
        .Id;

    [Fact]
    public void AskingForNoSocketTakesTheSocketTermOffTheScore()
    {
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Epic");
        var worn = new ItemDocument { Id = 6, Slot = "Chest", RuleSet = "Default_GemSlot" };

        var socketed = ItemPower.Inputs(worn, Settings, rung, new TooltipQuery(), 300);
        var bare = ItemPower.Inputs(worn, Settings, rung, new TooltipQuery { NoSocket = true }, 300);

        Assert.NotNull(socketed);
        Assert.NotNull(bare);
        Assert.True(socketed.HasGemSocket);
        Assert.False(bare.HasGemSocket);

        Assert.Equal(
            (float)Settings.Items.PowerPotential.GemSocketScoreFactor,
            ItemPower.Potential(socketed, Settings) - ItemPower.Potential(bare, Settings),
            3);
    }
}
