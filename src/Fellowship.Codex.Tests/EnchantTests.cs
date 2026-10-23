using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class EnchantTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private IReadOnlyList<EnchantDocument> Enchanted =>
        [.. export.Store.Entities.OfType<EnchantDocument>()];

    [Theory]
    [InlineData(1, 4)]
    [InlineData(149, 4)]
    [InlineData(150, 5)]
    [InlineData(299, 5)]
    [InlineData(300, 6)]
    [InlineData(420, 6)]
    public void Each_secondary_stat_an_enchant_grants_reads_the_curve_at_the_item_level(int itemLevel, int magnitude)
    {
        Assert.NotEmpty(Enchanted);

        Assert.All(Enchanted, enchant =>
        {
            var granted = AugmentationRanks.Granted(enchant, Settings, itemLevel).ToArray();

            Assert.Equal(2, granted.Length);
            Assert.All(granted, stat => Assert.Equal(magnitude, stat.Value));
        });
    }

    [Fact]
    public void A_stat_an_enchant_grants_at_no_coefficient_draws_no_line() =>
        Assert.All(Enchanted, enchant => Assert.Equal(
            enchant.Stats!.Count(stat => stat.Coefficient != 0),
            AugmentationRanks.Granted(enchant, Settings, Settings.Items.MaxItemLevel).Count()));

    [Theory]
    [InlineData(1, 3)]
    [InlineData(150, 4)]
    [InlineData(300, 5)]
    public void An_enchant_row_reads_one_below_the_power_its_item_level_gives(int itemLevel, int printed) =>
        Assert.Equal(printed, AugmentationRanks.RowValue(Settings, itemLevel));

    [Fact]
    public void An_enchant_on_the_item_adds_its_stats_to_the_stat_block()
    {
        var enchant = Enchanted[0];
        var item = export.Store.Entities
            .OfType<ItemDocument>()
            .First(row => row.Stats is { Count: > 0 } && row.Hero is not null);

        var plain = new TooltipQuery { ItemLevel = 300 };
        var enchanted = plain with { Modifiers = [new ItemModifier(ItemCraft.KindOfAugmentation(enchant, Settings)!.Value, enchant.Id, 1)] };

        var before = export.Tooltip.Rolls(item, plain, null);
        var after = export.Tooltip.Rolls(item, enchanted, null);

        foreach (var (stat, value) in AugmentationRanks.Granted(enchant, Settings, 300))
        {
            var was = before.SingleOrDefault(line => line.Stat == stat)?.Value ?? 0m;
            var line = after.Single(row => row.Stat == stat);

            Assert.Equal(was + value, line.Value);
            Assert.True(line.Rolled);
        }
    }

    [Fact]
    public void An_enchant_attuned_in_the_tree_adds_the_same_stats_as_one_rolled()
    {
        var enchant = Enchanted[0];
        var item = export.Store.Entities.OfType<ItemDocument>().First(row => row.Stats is { Count: > 0 });

        var rolled = export.Tooltip.Rolls(
            item, new TooltipQuery { ItemLevel = 300, Modifiers = [new ItemModifier(ItemCraft.KindOfAugmentation(enchant, Settings)!.Value, enchant.Id, 1)] }, null);
        var attuned = export.Tooltip.Rolls(
            item, new TooltipQuery { ItemLevel = 300, Attunements = [new Attunement(enchant.Id, 1)] }, null);

        Assert.Equal(rolled.Select(line => (line.Stat, line.Value)), attuned.Select(line => (line.Stat, line.Value)));
    }

    [Fact]
    public void Each_modifier_row_prints_the_value_its_widget_prints()
    {
        var item = export.Store.Entities.OfType<ItemDocument>().First(row => row.Stats is { Count: > 0 });
        var sigil = export.Store.Entities.OfType<SigilDocument>()
            .First(augmentation => !AugmentationRanks.IsEnchant(augmentation) && ItemCraft.KindOfAugmentation(augmentation, Settings) is not null);
        var glyph = export.Store.Entities.OfType<GlyphDocument>().First();
        var gem = export.Store.Entities.OfType<RadianceDocument>().First();

        var query = new TooltipQuery
        {
            ItemLevel = 300,
            Modifiers =
            [
                new ItemModifier(ItemCraft.KindOfAugmentation(Enchanted[0], Settings)!.Value, Enchanted[0].Id, 1),
                new ItemModifier(ItemCraft.KindOfAugmentation(sigil, Settings)!.Value, sigil.Id, 3),
                new ItemModifier(ItemCraft.KindOfSlotType(CraftSlots.AbilityRank, Settings)!.Value, glyph.Id, 3),
                new ItemModifier(ItemCraft.KindOfSlotType(CraftSlots.GemPower, Settings)!.Value, gem.Id, 40),
            ],
        };

        Assert.Equal(
            ["+5", null, TooltipView.GlyphRowValue, "+40"],
            export.Tooltip.Modifiers(item, query, null).Select(line => line.Value));
    }

    [Fact]
    public void A_constant_curve_holds_each_key_until_the_next_and_a_linear_curve_runs_between_keys()
    {
        CurvePoint[] keys = [new(1, 4), new(3, 6)];

        Assert.Equal(4, CurveReading.At(new Curve(CurveReading.Constant, keys), 2.5));
        Assert.Equal(5, CurveReading.At(new Curve(CurveReading.Linear, keys), 2));
        Assert.Equal(4, CurveReading.At(new Curve(CurveReading.Linear, keys), 0));
        Assert.Equal(6, CurveReading.At(new Curve(CurveReading.Linear, keys), 9));
    }

    [Fact]
    public void A_sigil_card_reads_its_category_text_and_its_description_at_the_equipped_rank()
    {
        var sigil = export.Store.Entities.OfType<SigilDocument>().First(augmentation => augmentation.RankRuns is { Count: > 0 });
        var run = sigil.RankRuns![0];
        var query = new TooltipQuery { RankSources = ["Head", "Weapon"] };

        Assert.Equal(("Sigil", "Augmentation"), (AugmentationRanks.Category(Settings, sigil).Heading, AugmentationRanks.Category(Settings, sigil).Band));
        Assert.Equal(2, AugmentationRanks.Rank(export.Store, query, sigil));

        var described = AugmentationRanks.Description(sigil, 2)!;
        Assert.Contains(run.Printed[1], described, StringComparison.Ordinal);
        Assert.DoesNotContain(run.Literal, described, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_rank_source_adds_one_items_rank_at_the_highest_item_level()
    {
        var enchant = Enchanted[0];
        var perItem = AugmentationRanks.Power(Settings, Settings.Items.MaxItemLevel);

        Assert.Equal(2 * perItem, AugmentationRanks.Rank(export.Store, new TooltipQuery { RankSources = ["Head", "Necklace"] }, enchant));
        Assert.Equal(perItem, AugmentationRanks.Rank(export.Store, new TooltipQuery(), enchant));
        Assert.Equal(["Head"], AugmentationRanks.Sources(export.Store, new TooltipQuery(), enchant).Select(source => source.Type));
    }

    [Fact]
    public void Two_items_between_item_level_150_and_299_reach_equipped_rank_10()
    {
        var query = new TooltipQuery { RankSources = ["Head", "Necklace"], ItemLevel = 200 };

        Assert.Equal(10, AugmentationRanks.Rank(export.Store, query, Enchanted[0]));
        Assert.Equal([(1, 149, 4), (150, 299, 5), (300, (int?)null, 6)], AugmentationRanks.RanksByItemLevel(Settings));
    }

    [Fact]
    public void A_single_source_augmentation_of_one_rank_states_no_rank()
    {
        var protective = export.Store.Entities.OfType<ProtectiveEnchantDocument>().First();

        Assert.False(AugmentationRanks.Ranked(export.Store, protective));
        Assert.True(AugmentationRanks.Ranked(export.Store, Enchanted[0]));
    }

    [Fact]
    public void Rank_sources_stop_at_the_max_rank()
    {
        var sigil = export.Store.Entities.OfType<SigilDocument>().First();
        var everySlot = new TooltipQuery
        {
            RankSources = [.. AugmentationRanks.Carriers(export.Store, sigil).Select(type => type.Type)],
        };

        Assert.Equal(4, sigil.MaxRank);
        Assert.Equal(4, AugmentationRanks.Limit(export.Store, everySlot, sigil));
        Assert.Equal(4, AugmentationRanks.Sources(export.Store, everySlot, sigil).Count);
        Assert.Equal(4, AugmentationRanks.Rank(export.Store, everySlot, sigil));
    }

    [Fact]
    public void Only_a_gear_slot_whose_items_can_carry_the_augmentation_is_a_rank_source()
    {
        string[] Carriers<T>() where T : AugmentationDocument =>
            [.. AugmentationRanks.Carriers(export.Store, export.Store.Entities.OfType<T>().First()).Select(type => type.Type)];

        Assert.DoesNotContain("Charm", Carriers<EnchantDocument>());
        Assert.Contains("Weapon", Carriers<EnchantDocument>());
        Assert.Equal(["Necklace"], Carriers<ProtectiveEnchantDocument>());
        Assert.Contains("Weapon", Carriers<SigilDocument>());
        Assert.DoesNotContain("Necklace", Carriers<SigilDocument>());
    }
}