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

        var aspect = Assert.Single(Type("MajorTrait").Recipes, recipe => recipe.Recipe == AspectRecipe);

        Assert.Equal(0, aspect.SlotIndex);
        Assert.Equal("Aspects", aspect.Category);
        Assert.Equal(1, aspect.Options);
        Assert.False(aspect.AllOptions);
    }

    [Fact]
    public void AModifierTypesLimitIsTheNumberItsWarningPrints()
    {
        var aspect = Type("MajorTrait");

        Assert.Equal("An item can have up to 2 Aspects active at a time.", aspect.MaximumLimitWarning);
        Assert.Equal(2, aspect.MaximumLimit);

        Assert.All(Export.Loaded.Settings.ModifierTypes, type => Assert.Equal(2, type.MaximumLimit));
    }

    [Fact]
    public void EveryModifierTypeIsASlotARecipeRerollsWithNineRecipes()
    {
        var types = Export.Loaded.Settings.ModifierTypes;

        Assert.Equal(
            ["AbilityRank", "GemPower", "HeroicTrait", "MajorTrait", "StatEnchant"],
            types.Select(type => type.Name).Order(StringComparer.Ordinal));
        Assert.All(types, type => Assert.Equal(9, type.Recipes.Count));
        Assert.All(types, type => Assert.Contains(type.Name, Export.Loaded.Settings.StatCalculation.SlotDefinitions.Keys));
        Assert.Equal(
            types.Select(type => type.Name).Order(StringComparer.Ordinal),
            Enum.GetNames<ModifierKind>().Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("HeroicTrait", "Sigil", "Sigils", "Sigil: ")]
    [InlineData("MajorTrait", "Aspect", "Aspects", "Aspect: ")]
    [InlineData("StatEnchant", "Enchant", "Enchants", "Enchant: ")]
    [InlineData("AbilityRank", "Glyph", "Glyphs", "Glyph:")]
    [InlineData("GemPower", "Radiance", null, "Radiance")]
    public void AModifierTypePrintsTheWordingItsCraftingCategoryNames(
        string name, string printed, string? plural, string header)
    {
        var type = Type(name);

        Assert.Equal(printed, type.Printed);
        Assert.Equal(plural, type.Plural);
        Assert.Equal(header, type.Header);
    }

    [Fact]
    public void AnEmptyModifierSlotAndTheTraitTreeReadTheirWidgetText()
    {
        Assert.Equal(new EmptyModifierSlot("?", "Empty Modifier"), Export.Loaded.Settings.EmptyModifierSlot);
        Assert.Equal("Attunement Tree:", Export.Loaded.Settings.TraitTreeHeader);
    }

    private static ModifierTypeEntry Type(string name) =>
        Export.Loaded.Settings.ModifierTypes.Single(type => type.Name == name);
}
