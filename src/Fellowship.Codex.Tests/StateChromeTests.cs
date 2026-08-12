namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class StateChromeTests(Export export)
{
    [Fact]
    public void A_slot_wears_the_chrome_its_rung_names()
    {
        var rung = export.Store.Settings.Rarities[0].Name;

        Assert.EndsWith(
            "T_Borders_EmptyWithoutFrame.png", export.Chrome.SlotBacking(rung), StringComparison.Ordinal);
        Assert.EndsWith(
            $"T_Borders_RarityFrame-{rung.ToLowerInvariant()}.png", export.Chrome.SlotFrame(rung),
            StringComparison.Ordinal);
        Assert.EndsWith("T_Borders_GlassFrame.png", export.Chrome.SlotBevel(rung), StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_AbilityBarBackground_Small.png", export.Chrome.SlotBacking(null),
            StringComparison.Ordinal);
        Assert.EndsWith(
            "T_Buttons_SmallAbilityButton_Default.png", export.Chrome.SlotFrame(null), StringComparison.Ordinal);
        Assert.Null(export.Chrome.SlotBevel(null));
    }

    [Fact]
    public void A_slot_with_nothing_in_it_wears_the_plate_the_widget_names()
    {
        Assert.EndsWith("T_Borders_EmptySlot.png", export.Chrome.SlotPlate, StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_answers_the_frame_the_state_names()
    {
        Assert.EndsWith(
            "T_Buttons_TraitNode_SelectedDefault.png", export.Chrome.TraitNode("selected"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_AvailableDefault.png", export.Chrome.TraitNode("available"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_TraitNode_UnavailableDefault.png", export.Chrome.TraitNode("unavailable"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_node_nothing_asks_for_is_the_one_the_reader_is_looking_at()
    {
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode(null));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("no such state"));
        Assert.Equal(export.Chrome.TraitNode("selected"), export.Chrome.TraitNode("SELECTED"));
    }

    [Fact]
    public void The_states_are_the_ones_the_build_ships_and_the_padlock_is_there()
    {
        Assert.Equal(["selected", "available", "unavailable"], ChromeAssetService.NodeStates);
        Assert.EndsWith("T_Icons_Locked.png", export.Chrome.LockGlyph, StringComparison.Ordinal);
        Assert.EndsWith("T_GoldDivider.png", export.Chrome.GoldDivider, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blessing_answers_the_plate_its_state_names()
    {
        Assert.Equal(["allocated", "unallocated"], ChromeAssetService.BlessingStates);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Allocated.png", export.Chrome.BlessingPlate("allocated"),
            StringComparison.Ordinal);

        Assert.EndsWith(
            "T_Buttons_BlessingButton_Unallocated.png", export.Chrome.BlessingPlate("unallocated"),
            StringComparison.Ordinal);

        Assert.Equal(export.Chrome.BlessingPlate("allocated"), export.Chrome.BlessingPlate(null));
    }

    [Fact]
    public void Every_blessing_answers_the_allocated_art_beside_its_unallocated_twin()
    {
        var blessings = export.Store.Entities.OfType<BlessingDocument>().ToArray();

        Assert.NotEmpty(blessings);

        Assert.All(blessings, blessing =>
        {
            var twin = blessing.Icon!.Replace("Unallocated", "Allocated", StringComparison.Ordinal);

            Assert.Equal(export.Chrome.Art(twin), export.Chrome.BlessingArt(blessing.Icon, null));
        });

        Assert.Null(export.Chrome.BlessingArt("Berserker16.png", null));
        Assert.Null(export.Chrome.BlessingArt(null, null));
    }
}
