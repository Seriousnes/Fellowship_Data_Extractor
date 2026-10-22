using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class EnchantTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private IReadOnlyList<TraitDocument> Enchanted =>
        [.. export.Store.Entities.OfType<TraitDocument>().Where(Enchants.Is)];

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
            var granted = Enchants.Granted(enchant, Settings, itemLevel).ToArray();

            Assert.Equal(2, granted.Length);
            Assert.All(granted, stat => Assert.Equal(magnitude, stat.Value));
        });
    }

    [Fact]
    public void A_stat_an_enchant_grants_at_no_coefficient_draws_no_line() =>
        Assert.All(Enchanted, enchant => Assert.Equal(
            enchant.Stats!.Count(stat => stat.Coefficient != 0),
            Enchants.Granted(enchant, Settings, Settings.Items.MaxItemLevel).Count()));

    [Theory]
    [InlineData(1, 0)]
    [InlineData(150, 1)]
    [InlineData(300, 2)]
    public void An_enchant_row_reads_one_below_the_power_its_item_level_gives(int itemLevel, int printed) =>
        Assert.Equal(printed, Enchants.RowValue(Settings, itemLevel));

    [Fact]
    public void An_enchant_on_the_item_adds_its_stats_to_the_stat_block()
    {
        var enchant = Enchanted[0];
        var item = export.Store.Entities
            .OfType<ItemDocument>()
            .First(row => row.Stats is { Count: > 0 } && row.Hero is not null);

        var plain = new TooltipQuery { ItemLevel = 300 };
        var enchanted = plain with { Modifiers = [new ItemModifier(ItemCraft.KindOfTrait(enchant, Settings)!.Value, enchant.Id, 1)] };

        var before = export.Tooltip.Rolls(item, plain, null);
        var after = export.Tooltip.Rolls(item, enchanted, null);

        foreach (var (stat, value) in Enchants.Granted(enchant, Settings, 300))
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
            item, new TooltipQuery { ItemLevel = 300, Modifiers = [new ItemModifier(ItemCraft.KindOfTrait(enchant, Settings)!.Value, enchant.Id, 1)] }, null);
        var attuned = export.Tooltip.Rolls(
            item, new TooltipQuery { ItemLevel = 300, ImbuedTraits = [new ImbuedTrait(enchant.Id, 1)] }, null);

        Assert.Equal(rolled.Select(line => (line.Stat, line.Value)), attuned.Select(line => (line.Stat, line.Value)));
    }

    [Fact]
    public void Each_modifier_row_prints_the_value_its_widget_prints()
    {
        var item = export.Store.Entities.OfType<ItemDocument>().First(row => row.Stats is { Count: > 0 });
        var sigil = export.Store.Entities.OfType<TraitDocument>()
            .First(trait => !Enchants.Is(trait) && ItemCraft.KindOfTrait(trait, Settings) is not null);
        var glyph = export.Store.Entities.OfType<BlessingDocument>().First();
        var gem = export.Store.Entities.OfType<GemTypeDocument>().First();

        var query = new TooltipQuery
        {
            ItemLevel = 300,
            Modifiers =
            [
                new ItemModifier(ItemCraft.KindOfTrait(Enchanted[0], Settings)!.Value, Enchanted[0].Id, 1),
                new ItemModifier(ItemCraft.KindOfTrait(sigil, Settings)!.Value, sigil.Id, 3),
                new ItemModifier(ItemCraft.KindOfSlotType(CraftSlots.AbilityRank, Settings)!.Value, glyph.Id, 3),
                new ItemModifier(ItemCraft.KindOfSlotType(CraftSlots.GemPower, Settings)!.Value, gem.Id, 40),
            ],
        };

        Assert.Equal(
            ["+2", null, TooltipView.GlyphRowValue, "+40"],
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
}
