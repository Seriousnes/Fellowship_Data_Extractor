using Fellowship.SDK.Client;
using Fellowship.SDK.Records;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public sealed class RecordReadingTests(Export export)
{
    private const string Origin = "https://codex.test";

    private IEntityStoreService Store => export.Store;

    private ItemDocument Rolled => Store.Entities
        .OfType<ItemDocument>()
        .First(item => item.Stats is { Count: > 0 } && item.Rarities is { Count: > 1 });

    private TRecord Read<TRecord>(EntityDocument document, RecordReading reading) where TRecord : EntityRecord =>
        RecordReadings.Read(
            RecordProjection.Of<TRecord>(document, export.View, Origin), document, reading, Store, export.Tooltip);

    [Fact]
    public void An_item_read_with_no_reading_has_no_magnitudes() =>
        Assert.Null(Read<ItemRecord>(Rolled, new RecordReading()).Magnitudes);

    [Fact]
    public void An_item_level_raises_the_stat_magnitudes()
    {
        var low = Read<ItemRecord>(Rolled, new RecordReading { ItemLevel = 100 }).Magnitudes!;
        var high = Read<ItemRecord>(Rolled, new RecordReading { ItemLevel = 400 }).Magnitudes!;

        Assert.Equal(100, low.ItemLevel);
        Assert.Equal(400, high.ItemLevel);
        Assert.True(high.Stats.Sum(stat => stat.Value) > low.Stats.Sum(stat => stat.Value));
    }

    [Fact]
    public void An_item_magnitude_is_the_tooltip_roll()
    {
        var query = new TooltipQuery { ItemLevel = 300, Upgrades = 2 };
        var rarity = export.Tooltip.Rarity(Rolled, query);

        var magnitudes = Read<ItemRecord>(Rolled, new RecordReading { ItemLevel = 300, Upgrades = 2 }).Magnitudes!;

        Assert.Equal(
            export.Tooltip.Rolls(Rolled, query, rarity).Select(line => line.Value),
            magnitudes.Stats.Select(stat => stat.Value));
    }

    [Fact]
    public void A_rarity_keeps_the_items_rolling_it()
    {
        var item = Rolled;
        var rolled = Store.Settings.Rarities.First(entry => entry.Tier == (int)item.Rarities![0]);
        var unrolled = Store.Settings.Rarities.First(entry => item.Rarities!.All(tier => (int)tier != entry.Tier));

        Assert.True(RecordReadings.Keeps(item, new RecordReading { Rarity = rolled.DisplayName }, Store));
        Assert.False(RecordReadings.Keeps(item, new RecordReading { Rarity = unrolled.DisplayName }, Store));
    }

    [Fact]
    public void An_enchant_rank_sums_its_rank_sources_and_its_stats_read_at_that_rank()
    {
        var enchant = Store.Entities.OfType<EnchantDocument>().First(row => row.Stats is { Count: > 0 });
        var reading = new RecordReading { ItemLevel = 300, RankSources = ["Head", "Chest"] };

        var equipped = Read<EnchantRecord>(enchant, reading).Equipped!;
        var perSource = AugmentationRanks.Power(Store.Settings, 300);

        Assert.Equal(2 * perSource, equipped.Rank);
        Assert.Equal(["Head", "Chest"], equipped.RankSources);
        Assert.All(equipped.Stats!, stat => Assert.Contains(
            $"+{stat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)} {stat.Stat}",
            equipped.Description));
    }

    [Fact]
    public void A_sigil_rank_counts_one_per_rank_source()
    {
        var sigil = Store.Entities.OfType<SigilDocument>().First(row => row.MaxRank is null or >= 2);

        var equipped = Read<SigilRecord>(sigil, new RecordReading { RankSources = ["Head", "Hands"] }).Equipped!;

        Assert.Equal(2, equipped.Rank);
        Assert.Null(equipped.Stats);
    }

    [Fact]
    public void A_stated_rank_reads_the_augmentation_at_that_rank_up_to_its_max()
    {
        var enchant = Store.Entities.OfType<EnchantDocument>().First(row => row.Stats is { Count: > 0 });

        var equipped = Read<EnchantRecord>(enchant, new RecordReading { Rank = 9 }).Equipped!;

        Assert.Equal(Math.Min(9, enchant.MaxRank ?? int.MaxValue), equipped.Rank);
        Assert.Empty(equipped.RankSources);
        Assert.Equal(
            AugmentationRanks.AtRank(enchant, Store.Settings, equipped.Rank).Select(stat => stat.Value),
            equipped.Stats!.Select(stat => stat.Value));
    }

    [Fact]
    public void A_rank_and_rank_sources_together_are_refused() =>
        Assert.NotNull(RecordReadings.Refused(new RecordReading { Rank = 2, RankSources = ["Head"] }, Store));

    [Fact]
    public void A_difficulty_scales_a_creature_health()
    {
        var creature = Store.Entities.OfType<CreatureDocument>().First(row => row.Health is > 0);
        var ladder = Difficulties.Ladder(Store.Settings);

        var low = Read<CreatureRecord>(creature, new RecordReading { Difficulty = ladder[0].Difficulty }).Difficulty!;
        var high = Read<CreatureRecord>(creature, new RecordReading { Difficulty = ladder[^1].Difficulty }).Difficulty!;

        Assert.Equal(ladder[^1].Label, high.Label);
        Assert.True(high.Health > low.Health);
    }

    [Fact]
    public void A_difficulty_scales_the_damage_of_an_ability_a_creature_owns()
    {
        var ability = Store.Entities.OfType<AbilityDocument>().First(row =>
            row.Coefficients is { Count: > 0 } && Store.Menacing(row) is not null);
        var ladder = Difficulties.Ladder(Store.Settings);

        var low = Read<AbilityRecord>(ability, new RecordReading { Difficulty = ladder[0].Difficulty }).Difficulty!;
        var high = Read<AbilityRecord>(ability, new RecordReading { Difficulty = ladder[^1].Difficulty }).Difficulty!;

        Assert.NotEmpty(high.Damage);
        Assert.True(high.Damage[0].Mean > low.Damage[0].Mean);
    }

    [Fact]
    public void A_glyph_reads_at_its_equipped_tier()
    {
        var glyph = Store.Entities.OfType<GlyphDocument>().First(row => row.Description is not null);

        var equipped = Read<GlyphRecord>(glyph, new RecordReading { EquippedTier = 2 }).Equipped!;

        Assert.Equal(2, equipped.Tier);
        Assert.Equal(Store.Settings.Glyphs.Tiers, equipped.Tiers);
        Assert.False(string.IsNullOrWhiteSpace(equipped.Description));
        Assert.DoesNotContain("<", equipped.Description);
    }

    [Fact]
    public void A_tier_past_the_glyph_tiers_is_refused() =>
        Assert.NotNull(RecordReadings.Refused(
            new RecordReading { EquippedTier = Store.Settings.Glyphs.Tiers + 1 }, Store));

    [Theory]
    [InlineData(0, null, null, null)]
    [InlineData(null, "Shiny", null, null)]
    [InlineData(null, null, "Tail", null)]
    [InlineData(null, null, null, 999)]
    public void A_reading_outside_the_build_is_refused(int? itemLevel, string? rarity, string? source, int? difficulty) =>
        Assert.NotNull(RecordReadings.Refused(
            new RecordReading
            {
                ItemLevel = itemLevel,
                Rarity = rarity,
                RankSources = source is null ? null : [source],
                Difficulty = difficulty,
            },
            Store));

    [Fact]
    public void The_highest_item_level_is_read() =>
        Assert.Null(RecordReadings.Refused(new RecordReading { ItemLevel = Store.Settings.Items.MaxItemLevel }, Store));
}
