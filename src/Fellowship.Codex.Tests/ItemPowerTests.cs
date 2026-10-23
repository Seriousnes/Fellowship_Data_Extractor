namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class ItemPowerTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private static ItemPowerInputs Bare(string slot) =>
        new(slot, Level: 0, MaxUpgrades: 0, RerollableSlots: 0,
            Legendary: false, HasGemSocket: false, HasGrantedAbility: false,
            HasAttunementTree: false);

    private float Of(ItemPowerInputs given) => ItemPower.Potential(given, Settings);

    private string Printed(string type) =>
        ItemPower.Printed(Settings.Items, type)
        ?? throw new InvalidOperationException($"The settings name no slot for {type}.");

    private RarityEntry Regal => Settings.Rarities.Single(rarity => rarity.DisplayName == "Regal");

    private ItemDocument Rolled(string slot) => export.Store.Entities
        .OfType<ItemDocument>()
        .First(item => item.Slot == slot && item.RuleSet is not null);

    private ItemDocument Socketed => export.Store.Entities
        .OfType<ItemDocument>()
        .First(item => item.RuleSet is { } rule &&
            Settings.StatCalculation.RarityAdditions.TryGetValue(rule, out var byTier) &&
            byTier.Values.Any(added => added.GuaranteedSlots.Any(name =>
                ItemCraft.Definition(name, Settings).SlotType == CraftSlots.GemSocket)));

    [Fact]
    public void Each_slot_weight_is_the_patch_note_share_of_the_stat_budget()
    {
        Assert.Equal("Neck", Printed(ItemPower.NecklaceType));
        Assert.Equal("Relic", Printed(ItemPower.TrinketType));
        Assert.Equal("Weapon", Printed(ItemPower.WeaponType));

        var head = Settings.StatCalculation.SlotWeights["Head"].StatWeight;

        Assert.Equal(head, Settings.StatCalculation.SlotWeights["Legs"].StatWeight, 6);
        Assert.Equal(head * 0.6, Settings.StatCalculation.SlotWeights["Relic"].StatWeight, 6);
        Assert.Equal(head * 1.5, Settings.StatCalculation.SlotWeights["Weapon"].StatWeight, 6);
    }

    [Fact]
    public void A_granted_ability_scores_on_a_weapon_and_a_relic_and_nowhere_else()
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
    public void A_attunement_tree_scores_on_a_weapon_and_a_neck_and_nowhere_else()
    {
        var factors = Settings.Items.PowerPotential;

        foreach (var slot in Settings.StatCalculation.SlotWeights.Keys)
        {
            var without = Of(Bare(slot));
            var with = Of(Bare(slot) with { HasAttunementTree = true });

            var expected = slot == Printed(ItemPower.WeaponType)
                ? (float)factors.WeaponAttunementTreeScoreFactor
                : slot == Printed(ItemPower.NecklaceType)
                    ? (float)factors.NecklaceAttunementTreeScoreFactor
                    : 0f;

            Assert.Equal(expected, with - without, 3);
        }
    }

    [Fact]
    public void An_item_whose_slot_the_weights_do_not_cover_scores_nothing()
    {
        var unworn = new ItemDocument { Id = 1, Slot = "Sporran" };

        Assert.Null(ItemPower.Inputs(unworn, Settings, null, new TooltipQuery(), 300));
        Assert.Null(ItemPower.Of(unworn, Settings, null, new TooltipQuery(), 300));
    }

    [Fact]
    public void An_item_with_no_slot_or_no_level_scores_nothing()
    {
        var slotless = new ItemDocument { Id = 2 };
        var worn = new ItemDocument { Id = 3, Slot = "Chest" };

        Assert.Null(ItemPower.Of(slotless, Settings, null, new TooltipQuery(), 300));
        Assert.Null(ItemPower.Of(worn, Settings, null, new TooltipQuery(), null));
    }

    [Fact]
    public void A_regal_weapon_scores_its_ability_its_attunement_tree_and_its_modifier_slots()
    {
        var weapon = Rolled(Printed(ItemPower.WeaponType));
        var added = Settings.StatCalculation.RarityAdditions[weapon.RuleSet!][Regal.Tier];

        var given = ItemPower.Inputs(weapon, Settings, Regal, new TooltipQuery(), 300);

        Assert.NotNull(given);
        Assert.True(given.HasGrantedAbility);
        Assert.True(given.HasAttunementTree);
        Assert.False(given.HasGemSocket);
        Assert.Equal(added.RandomRolls, given.RerollableSlots);
    }

    [Fact]
    public void Asking_for_no_socket_takes_the_socket_term_off_the_score()
    {
        var rung = Settings.Rarities.Single(rarity => rarity.Name == "Epic");
        var worn = Socketed;

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

    [Fact]
    public void A_necklace_scores_its_attunement_tree_flat_so_an_unallocated_tree_scores_as_a_full_one()
    {
        var necklace = Rolled(Printed(ItemPower.NecklaceType));

        var given = ItemPower.Inputs(necklace, Settings, Regal, new TooltipQuery(), 300);
        var allocated = ItemPower.Inputs(necklace, Settings, Regal, new TooltipQuery { AllocatedAttunements = 2 }, 300);

        Assert.NotNull(given);
        Assert.NotNull(allocated);
        Assert.True(given.HasAttunementTree);
        Assert.Equal(ItemPower.Potential(given, Settings), ItemPower.Potential(allocated, Settings));
    }
}
