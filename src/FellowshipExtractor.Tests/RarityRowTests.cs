using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class RarityRowTests
{
    private const string RuleSetTable =
        "fellowship/Plugins/GameFeatures/DynamicLootSystem/Content/DT_DynamicHeroItemLootRarityRuleSets.DT_DynamicHeroItemLootRarityRuleSets";

    private const string RelicRuleSet = "DynamicHeroItemRuleSet.Relic";

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

    [Fact]
    public void TheLastRowNamingARuleSetAndRarityIsTheOneItRolls()
    {
        var uncommon = Tier("Uncommon");
        var rows = Export.Provider.LoadPackageObject<UDataTable>(RuleSetTable).RowMap
            .Where(entry => PropertyWalk.AsOrdinal(PropertyWalk.Get(entry.Value, "RarityTier")?.GenericValue) == uncommon)
            .Where(entry => Listed(entry.Value, "RuleSets").Contains(RelicRuleSet))
            .ToArray();

        Assert.True(rows.Length > 1);
        Assert.Equal("Uncommon_Weapon", rows[^1].Key.Text);

        var relic = Additions["Relic"][uncommon];
        Assert.Equal(Listed(rows[^1].Value, "AdditionalGuaranteedSlots"), relic.GuaranteedSlots);
        Assert.Contains("WeaponPower", relic.GuaranteedSlots);

        var placeholder = Additions["Default_GemSlot"][uncommon];
        Assert.Empty(placeholder.GuaranteedSlots);
        Assert.Empty(placeholder.RandomizedSlots);
    }

    private static IReadOnlyList<string> Listed(IPropertyHolder row, string field) =>
        PropertyWalk.Get(row, field)?.GenericValue is UScriptArray listed
            ? [.. listed.Properties.Select(entry => PropertyWalk.AsString(entry?.GenericValue)).OfType<string>()]
            : [];
}
