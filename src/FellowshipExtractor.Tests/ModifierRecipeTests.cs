using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ModifierRecipeTests
{
    private const string RecipeTable =
        "fellowship/Plugins/GameFeatures/DynamicLootSystem/Content/DT_DynamicHeroItemCraftingRecipes.DT_DynamicHeroItemCraftingRecipes";

    private const string AspectRecipe = "Crafting.Recipes.HeroItems.Slots.RerollSlotIntoSpecific.MasterTrait.Slot1OneOption";

    [Fact]
    public void ARerollRecipeNamesTheSlotDefinitionItRerolls()
    {
        var row = Export.Provider.LoadPackageObject<UDataTable>(RecipeTable).RowMap
            .Single(entry => entry.Key.Text == AspectRecipe).Value;

        var data = Assert.IsAssignableFrom<IPropertyHolder>(
            Assert.IsType<FInstancedStruct>(
                Assert.IsType<FScriptStruct>(PropertyWalk.Get(row, "RecipeSpecificData")?.GenericValue).StructType)
                .NonConstStruct);

        Assert.Equal("MajorTrait", PropertyWalk.GetString(data, "ModifierType"));

        var aspect = Assert.Single(
            Export.Loaded.Settings.StatCalculation.SlotDefinitions["MajorTrait"].Recipes,
            recipe => recipe.Recipe == AspectRecipe);

        Assert.Equal(0, aspect.SlotIndex);
        Assert.Equal("Aspects", aspect.Category);
        Assert.Equal(1, aspect.Options);
        Assert.False(aspect.AllOptions);
    }

    [Fact]
    public void AModifierSlotsLimitIsTheNumberItsWarningPrints()
    {
        var aspect = Export.Loaded.Settings.StatCalculation.SlotDefinitions["MajorTrait"];

        Assert.Equal("An item can have up to 2 Aspects active at a time.", aspect.MaximumLimitWarning);
        Assert.Equal(2, aspect.MaximumLimit);

        Assert.All(
            Export.Loaded.Settings.StatCalculation.SlotDefinitions.Values.Where(slot => slot.Recipes.Count > 0),
            slot => Assert.Equal(2, slot.MaximumLimit));
    }

    [Fact]
    public void EveryModifierSlotARecipeRerollsListsNineRecipes()
    {
        var rerolled = Export.Loaded.Settings.StatCalculation.SlotDefinitions
            .Where(slot => slot.Value.Recipes.Count > 0)
            .ToDictionary(slot => slot.Key, slot => slot.Value.Recipes.Count);

        Assert.Equal(["AbilityRank", "GemPower", "HeroicTrait", "MajorTrait", "StatEnchant"], rerolled.Keys.Order(StringComparer.Ordinal));
        Assert.All(rerolled.Values, count => Assert.Equal(9, count));
    }
}
