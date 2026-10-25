namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CraftingOddsTests(Export export)
{
    private const string ChooseAnyAspect = "Crafting.Recipes.HeroItems.Slots.RerollSlotIntoSpecific.MasterTrait.Slot1AllOptions";

    [Fact]
    public void One_offer_from_equal_weights_is_one_in_the_pool()
    {
        Assert.Equal(1d / 8, CraftingOdds.Offered([1, 1, 1, 1, 1, 1, 1, 1], 0, 1, ItemCraft.SameIdentity), 10);
    }

    [Fact]
    public void Three_offers_hold_at_most_two_of_one_modifier()
    {
        Assert.Equal(20d / 27, CraftingOdds.Offered([1, 1, 1], 0, 3, 2), 10);
    }

    [Fact]
    public void A_two_modifier_pool_offers_the_target_in_three_offers()
    {
        Assert.Equal(1, CraftingOdds.Offered([1, 1], 0, 3, 2), 10);
    }

    [Fact]
    public void Choose_any_is_certain()
    {
        var reroll = new ModifierRecipe(ChooseAnyAspect, 0) { AllOptions = true, Options = 1 };

        Assert.Equal(1, CraftingOdds.Chance([1, 1, 1], 0, reroll));
    }

    [Fact]
    public void Every_modifier_type_rolls_on_a_gear_slot_with_its_recipes_named_in_the_settings()
    {
        foreach (var type in export.Store.Settings.ModifierTypes)
        {
            var kind = ItemCraft.KindOf(type.Name, export.Store.Settings)!.Value;
            var slots = CraftingOdds.GearSlots(kind, export.Store);

            Assert.NotEmpty(slots);

            var pool = CraftingOdds.Pool(kind, slots[0], export.Store);
            var attempts = CraftingOdds.Attempts(kind, 0, pool, 0, export.Store.Settings);

            Assert.Equal(3, attempts.Count);
            Assert.All(attempts, attempt => Assert.InRange(attempt.Chance, double.Epsilon, 1));
        }
    }

    [Fact]
    public void Aspects_roll_on_the_gear_slots_whose_rule_sets_roll_them()
    {
        var slots = CraftingOdds.GearSlots(ModifierKind.MajorTrait, export.Store);

        Assert.Contains("Head", slots);
        Assert.DoesNotContain("Ring", slots);
        Assert.DoesNotContain("Neck", slots);
    }
}
