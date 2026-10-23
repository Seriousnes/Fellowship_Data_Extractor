using Fellowship.SDK.Models;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class AugmentationCategoryTextTests
{
    private static readonly Lazy<IReadOnlyList<AugmentationCategoryText>> Read = new(() =>
    {
        var assets = AssetRegistry.ReadAssets(Export.Provider).ToList();

        return new TooltipWidgetText(Export.Provider, assets, new ItemSlotNames(Export.Provider, assets))
            .AugmentationTooltip.Categories;
    }, isThreadSafe: true);

    [Fact]
    public void TheAugmentationTooltipChoosesItsHeadingAndBandOnFourCategoriesInTheOrderItTestsThem()
    {
        Assert.Equal(
            [
                "ItemTrait.TraitCategory.StatEnchant",
                "ItemTrait.TraitCategory.Necklace",
                "ItemTrait.TraitCategory.Heroic",
                "ItemTrait.TraitCategory.Major",
                null,
            ],
            Read.Value.Select(category => category.CategoryTag));
    }

    [Theory]
    [InlineData("ItemTrait.TraitCategory.StatEnchant", "Item_Feature_Enchant_Name", "Item_Feature_Enchant_Name")]
    [InlineData("ItemTrait.TraitCategory.Necklace", "Item_Feature_NeckEnchant_Name", "Item_Feature_Enchant_Name")]
    [InlineData("ItemTrait.TraitCategory.Heroic", "Item_Feature_Sigil_Name", "Item_Feature_Augmentation_Name")]
    [InlineData("ItemTrait.TraitCategory.Major", "Item_Feature_Aspect_Name", "Item_Feature_Augmentation_Name")]
    [InlineData(null, "Item_Feature_Augmentation_Name", "Item_Feature_Augmentation_Name")]
    public void EachCategoryPrintsTheItemsTableTextsTheWidgetSets(string? tag, string heading, string band)
    {
        var category = Read.Value.Single(category => category.CategoryTag == tag);

        Assert.Equal(StringTables.Find("ST_Items", heading), category.Heading);
        Assert.Equal(StringTables.Find("ST_Items", band), category.Band);
    }

    [Fact]
    public void AnEnchantIsHeadedAndBandedEnchant()
    {
        var enchant = Read.Value.Single(category => category.CategoryTag == "ItemTrait.TraitCategory.StatEnchant");

        Assert.Equal("Enchant", enchant.Heading);
        Assert.Equal("Enchant", enchant.Band);
    }
}
