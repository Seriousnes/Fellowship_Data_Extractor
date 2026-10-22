namespace FellowshipExtractor.Tests;

public sealed class RarityRowTests
{
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, RaritySlots>> Additions =>
        Export.Loaded.Settings.StatCalculation.RarityAdditions;

    private static int Tier(string name) => Export.Loaded.Settings.Rarities.Single(rung => rung.Name == name).Tier;

    private static IReadOnlySet<string> CarriedRuleSets =>
        Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Item)
            .Select(row => row.Body["ruleSet"]?.GetValue<string>())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void TheRuleSetsAreTheOnesItemsCarry() =>
        Assert.Equal(CarriedRuleSets.Order(StringComparer.Ordinal), Additions.Keys.Order(StringComparer.Ordinal));

    [Fact]
    public void NoRuleSetRollsAtARarityThatNeverDrops()
    {
        var champion = Tier("Champion");

        Assert.All(Additions.Values, byTier => Assert.DoesNotContain(champion, byTier.Keys));
    }

    [Theory]
    [InlineData("Rare", 1)]
    [InlineData("Epic", 2)]
    [InlineData("Heroic", 3)]
    public void ADefaultItemRollsTheModifiersThePatchStates(string rarity, int rolls) =>
        Assert.Equal(rolls, Additions["Default_NoGemSlot"][Tier(rarity)].RandomRolls);

    [Fact]
    public void AnUncommonItemRollsNoModifier() =>
        Assert.All(Additions.Values, byTier =>
        {
            if (byTier.TryGetValue(Tier("Uncommon"), out var uncommon)) Assert.Equal(0, uncommon.RandomRolls);
        });

    [Fact]
    public void AHeroicWeaponRollsTwoModifiers() => Assert.Equal(2, Additions["Weapon"][Tier("Heroic")].RandomRolls);

    [Fact]
    public void EverySlotARarityRowNamesHasItsDefinition()
    {
        var definitions = Export.Loaded.Settings.StatCalculation.SlotDefinitions;

        Assert.All(
            Additions.Values.SelectMany(byTier => byTier.Values)
                .SelectMany(slots => slots.GuaranteedSlots.Concat(slots.RandomizedSlots)),
            slot => Assert.Contains(slot, definitions.Keys));

        Assert.Equal("ItemTrait", definitions["StatEnchant"].SlotType);
        Assert.Equal(["ItemTrait.TraitCategory.StatEnchant"], definitions["StatEnchant"].ItemTraitPool);
        Assert.Equal(["ItemTrait.TraitCategory.Heroic"], definitions["HeroicTrait"].ItemTraitPool);
        Assert.Equal(["ItemTrait.TraitCategory.Major"], definitions["MajorTrait"].ItemTraitPool);
        Assert.Equal(120, definitions["AbilityRank"].MinimumItemLevel);
        Assert.Equal(6, definitions["GemPower"].GemPowerPool.Count);
    }

    [Fact]
    public void NoRuleSetNamesASetBonusOrARandomStat() =>
        Assert.All(Additions.Values.SelectMany(byTier => byTier.Values), slots =>
        {
            Assert.DoesNotContain("SetBonus", slots.GuaranteedSlots);
            Assert.DoesNotContain("RandomStat", slots.GuaranteedSlots);
            Assert.DoesNotContain("RandomStat", slots.RandomizedSlots);
        });
}
