namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class ItemCraftTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private IReadOnlyList<CraftSlot> Slots(string ruleSet, int tier, string? worn = null) =>
        ItemCraft.Slots(
            new ItemDocument { Id = 1, RuleSet = ruleSet, Slot = worn },
            Settings,
            Settings.Rarities.SingleOrDefault(rung => rung.Tier == tier));

    [Fact]
    public void Every_rule_set_the_settings_state_derives_slots_at_every_tier_it_states()
    {
        foreach (var (ruleSet, byTier) in Settings.StatCalculation.RarityAdditions)
        {
            foreach (var (tier, added) in byTier)
            {
                var slots = Slots(ruleSet, tier);

                Assert.Equal(
                    added.GuaranteedSlots.Count + added.SpecialSlots.Count + added.RandomRolls,
                    slots.Count);
            }
        }
    }

    [Fact]
    public void A_guaranteed_slot_keeps_the_name_the_table_gives_it_and_its_order()
    {
        foreach (var (ruleSet, byTier) in Settings.StatCalculation.RarityAdditions)
        {
            foreach (var (tier, added) in byTier)
            {
                var slots = Slots(ruleSet, tier);

                Assert.Equal(
                    added.GuaranteedSlots,
                    [.. slots.Take(added.GuaranteedSlots.Count).Select(slot => slot.SlotName)]);
            }
        }
    }

    [Fact]
    public void Two_slots_of_one_name_are_told_apart_by_their_ordinal()
    {
        foreach (var (ruleSet, byTier) in Settings.StatCalculation.RarityAdditions)
        {
            foreach (var tier in byTier.Keys)
            {
                foreach (var group in Slots(ruleSet, tier).GroupBy(slot => slot.SlotName, StringComparer.OrdinalIgnoreCase))
                {
                    Assert.Equal(
                        [.. Enumerable.Range(0, group.Count())],
                        [.. group.Select(slot => slot.Ordinal).Order()]);
                }
            }
        }
    }

    [Fact]
    public void A_roll_accepts_the_distinct_kinds_the_table_randomises()
    {
        foreach (var (ruleSet, byTier) in Settings.StatCalculation.RarityAdditions)
        {
            foreach (var (tier, added) in byTier)
            {
                if (added.RandomRolls == 0) continue;

                var rolls = Slots(ruleSet, tier)
                    .Where(slot => slot.SlotName == CraftSlots.RandomRolls)
                    .ToArray();

                Assert.Equal(added.RandomRolls, rolls.Length);

                Assert.All(rolls, roll => Assert.Equal(
                    [.. added.RandomizedSlots.Distinct(StringComparer.OrdinalIgnoreCase)],
                    [.. roll.Accepts]));
            }
        }
    }

    [Fact]
    public void A_special_slot_the_table_names_becomes_a_slot_of_its_own()
    {
        var rung = Settings.Rarities[^1];
        var named = Settings.StatCalculation.RarityAdditions.Values
            .SelectMany(byTier => byTier.Values)
            .SelectMany(added => added.GuaranteedSlots)
            .First();

        var staged = Settings with
        {
            StatCalculation = Settings.StatCalculation with
            {
                RarityAdditions = new Dictionary<string, IReadOnlyDictionary<int, RaritySlots>>
                {
                    [nameof(SpecialSlot)] = new Dictionary<int, RaritySlots>
                    {
                        [rung.Tier] = new(rung.Tier, [], 0, []) { SpecialSlots = [new SpecialSlot(named, null)] },
                    },
                },
            },
        };

        var slots = ItemCraft.Slots(
            new ItemDocument { Id = 1, RuleSet = nameof(SpecialSlot) }, staged, rung);

        Assert.Equal([named], slots.Select(slot => slot.SlotName));
    }

    [Fact]
    public void An_item_naming_no_rule_set_or_asked_at_no_rarity_derives_nothing()
    {
        Assert.Empty(ItemCraft.Slots(new ItemDocument { Id = 1 }, Settings, Settings.Rarities[^1]));

        Assert.Empty(ItemCraft.Slots(
            new ItemDocument { Id = 1, RuleSet = Settings.StatCalculation.RarityAdditions.Keys.First() },
            Settings,
            rarity: null));
    }

    [Fact]
    public void The_three_modifier_kinds_map_onto_the_three_modifier_kinds_the_api_documents()
    {
        Assert.Equal(ModifierKind.Trait, ItemCraft.KindOf(CraftSlots.ItemTrait));
        Assert.Equal(ModifierKind.Blessing, ItemCraft.KindOf(CraftSlots.AbilityRank));
        Assert.Equal(ModifierKind.Essence, ItemCraft.KindOf(CraftSlots.GemPower));
        Assert.Null(ItemCraft.KindOf(CraftSlots.RandomStat));

        Assert.Equal(3, Enum.GetValues<ModifierKind>().Length);
    }
}
