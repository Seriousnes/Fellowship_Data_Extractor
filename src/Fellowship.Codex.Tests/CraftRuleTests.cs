namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CraftRuleTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private IReadOnlyList<BlessingDocument> Blessings =>
        [.. export.Store.Entities.OfType<BlessingDocument>()];

    private IReadOnlyList<TraitDocument> Traits =>
        [.. export.Store.Entities.OfType<TraitDocument>()];

    private static CraftSlot Roll(params string[] accepts) => new(CraftSlots.RandomRolls, 0, accepts);

    private static ItemDocument Worn(string slot) =>
        new() { Id = 1, Slot = slot, RuleSet = "Default_NoGemSlot" };

    private string? Forbids(ItemDocument item, TooltipQuery query, CraftSlot slot, CraftCandidate candidate) =>
        ItemCraft.Forbids(item, query, slot, candidate, export.Store);

    [Fact]
    public void A_stat_rolled_twice_already_is_forbidden_a_third_time()
    {
        var stat = Settings.StatCalculation.RandomStatPool[0];
        var slot = new CraftSlot(CraftSlots.RandomStat, 2, [CraftSlots.RandomStat]);
        var candidate = new CraftCandidate(CraftSlots.RandomStat, 0, stat, 1);

        Assert.Null(Forbids(
            Worn("Head"), new TooltipQuery { RandomStats = [stat] }, slot, candidate));

        Assert.Equal(
            CraftReasons.TwoOfAKind,
            Forbids(Worn("Head"), new TooltipQuery { RandomStats = [stat, stat] }, slot, candidate));
    }

    [Fact]
    public void A_stat_the_item_declares_does_not_count_towards_the_two_of_a_kind_cap()
    {
        var stat = Settings.StatCalculation.RandomStatPool[0];

        var item = new ItemDocument
        {
            Id = 5213,
            Slot = "Head",
            RuleSet = "Default_NoGemSlot",
            Stats = [new StatSlot(stat, 40) { Kind = SlotKinds.Primary }],
        };

        Assert.Null(Forbids(
            item,
            new TooltipQuery { RandomStats = [stat] },
            new CraftSlot(CraftSlots.RandomStat, 1, [CraftSlots.RandomStat]),
            new CraftCandidate(CraftSlots.RandomStat, 0, stat, 1)));
    }

    [Fact]
    public void A_third_instance_of_one_trait_id_is_forbidden()
    {
        var trait = Traits[0];
        var already = new ItemModifier(ModifierKind.Trait, trait.Id, 1);
        var candidate = new CraftCandidate(CraftSlots.ItemTrait, trait.Id, trait.Name ?? string.Empty, 1);

        Assert.Null(Forbids(
            Worn("Head"), new TooltipQuery { Modifiers = [already] }, Roll(CraftSlots.ItemTrait), candidate));

        Assert.Equal(
            CraftReasons.TwoOfAKind,
            Forbids(
                Worn("Head"),
                new TooltipQuery { Modifiers = [already, already] },
                Roll(CraftSlots.ItemTrait),
                candidate));
    }

    [Fact]
    public void A_third_blessing_is_forbidden_at_every_tier()
    {
        var offered = Blessings.Where(blessing => blessing.Slots is { Count: > 0 }).Take(3).ToArray();

        Assert.Equal(3, offered.Length);

        var worn = offered[2].Slots![0];

        var carried = offered
            .Take(2)
            .Select(blessing => new ItemModifier(ModifierKind.Blessing, blessing.Id, 1))
            .ToArray();

        Assert.Equal(
            CraftReasons.TwoBlessings,
            Forbids(
                Worn(worn),
                new TooltipQuery { Modifiers = carried },
                Roll(CraftSlots.AbilityRank),
                new CraftCandidate(CraftSlots.AbilityRank, offered[2].Id, offered[2].Name ?? string.Empty, 1)));
    }

    [Fact]
    public void A_blessing_offers_only_on_a_slot_its_pools_name()
    {
        var blessing = Blessings.First(row => row.Slots is { Count: > 0 });

        var candidate = new CraftCandidate(CraftSlots.AbilityRank, blessing.Id, blessing.Name ?? string.Empty, 1);

        Assert.All(blessing.Slots!, worn =>
            Assert.Null(Forbids(Worn(worn), new TooltipQuery(), Roll(CraftSlots.AbilityRank), candidate)));

        const string Elsewhere = "Offhand";

        Assert.DoesNotContain(Elsewhere, blessing.Slots!, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            CraftReasons.NotInPool(Elsewhere),
            Forbids(Worn(Elsewhere), new TooltipQuery(), Roll(CraftSlots.AbilityRank), candidate));
    }

    [Fact]
    public void A_blessing_runs_from_one_to_the_rank_its_row_states()
    {
        var blessing = Blessings.First(row => row.Slots is { Count: > 0 } && row.MaxRank is > 0);
        var worn = blessing.Slots![0];

        string? At(int rank) => Forbids(
            Worn(worn),
            new TooltipQuery(),
            Roll(CraftSlots.AbilityRank),
            new CraftCandidate(CraftSlots.AbilityRank, blessing.Id, blessing.Name ?? string.Empty, rank));

        Assert.Null(At(1));
        Assert.Null(At(blessing.MaxRank!.Value));
        Assert.Equal(CraftReasons.PastRank(blessing.MaxRank.Value), At(blessing.MaxRank.Value + 1));
        Assert.Equal(CraftReasons.PastRank(blessing.MaxRank.Value), At(0));
    }

    [Fact]
    public void A_trait_runs_from_one_to_the_rank_its_document_states()
    {
        var trait = Traits.First(row => row.MaxRank is > 0);

        string? At(int rank) => Forbids(
            Worn("Head"),
            new TooltipQuery(),
            Roll(CraftSlots.ItemTrait),
            new CraftCandidate(CraftSlots.ItemTrait, trait.Id, trait.Name ?? string.Empty, rank));

        Assert.Null(At(trait.MaxRank!.Value));
        Assert.Equal(CraftReasons.PastRank(trait.MaxRank.Value), At(trait.MaxRank.Value + 1));
    }

    [Fact]
    public void An_imbued_trait_sits_once_and_only_while_a_slot_is_free()
    {
        var worn = TooltipView.ImbuedTraitCapacities.Keys.First();
        var capacity = TooltipView.ImbuedTraitCapacities[worn];
        var item = Worn(worn);
        var block = new CraftSlot(CraftSlots.TraitTree, 0, [CraftSlots.ItemTrait]) { Capacity = capacity };

        var chosen = Traits.Where(row => row.MaxRank is > 0).Take(capacity + 1).ToArray();

        Assert.True(chosen.Length == capacity + 1);

        var filled = chosen.Take(capacity).Select(row => new ImbuedTrait(row.Id, 1)).ToArray();

        Assert.Equal(
            CraftReasons.AlreadyImbued,
            Forbids(
                item,
                new TooltipQuery { ImbuedTraits = [filled[0]] },
                block,
                new CraftCandidate(CraftSlots.ItemTrait, chosen[0].Id, chosen[0].Name ?? string.Empty, 1)));

        Assert.Equal(
            CraftReasons.NoRoom,
            Forbids(
                item,
                new TooltipQuery { ImbuedTraits = filled },
                block,
                new CraftCandidate(CraftSlots.ItemTrait, chosen[^1].Id, chosen[^1].Name ?? string.Empty, 1)));
    }

    [Fact]
    public void A_slot_does_not_count_what_it_already_holds_against_the_choice_replacing_it()
    {
        var offered = Blessings.Where(blessing => blessing.Slots is { Count: > 0 }).Take(2).ToArray();
        var worn = offered[0].Slots![0];

        var carried = offered
            .Select(blessing => new ItemModifier(ModifierKind.Blessing, blessing.Id, 1))
            .ToArray();

        var query = new TooltipQuery { Modifiers = carried };
        var slot = Roll(CraftSlots.AbilityRank);
        var candidate = new CraftCandidate(CraftSlots.AbilityRank, offered[0].Id, offered[0].Name ?? string.Empty, 1);

        Assert.Equal(CraftReasons.TwoBlessings, Forbids(Worn(worn), query, slot, candidate));

        Assert.Null(Forbids(Worn(worn), ItemCraft.Without(query, slot), slot, candidate));
    }

    [Fact]
    public void A_slot_the_candidate_fills_states_the_query_it_would_become()
    {
        var stat = Settings.StatCalculation.RandomStatPool[0];
        var other = Settings.StatCalculation.RandomStatPool[1];

        var slot = new CraftSlot(CraftSlots.RandomStat, 1, [CraftSlots.RandomStat]);

        var filled = ItemCraft.With(
            new TooltipQuery { RandomStats = [stat, other] },
            slot,
            new CraftCandidate(CraftSlots.RandomStat, 0, stat, 1));

        Assert.Equal([stat, stat], filled.RandomStats);

        var trait = Traits[0];

        var swapped = ItemCraft.With(
            new TooltipQuery { Modifiers = [new ItemModifier(ModifierKind.Blessing, 1, 1)] },
            Roll(CraftSlots.ItemTrait),
            new CraftCandidate(CraftSlots.ItemTrait, trait.Id, trait.Name ?? string.Empty, 2));

        Assert.Equal(
            new ItemModifier(ModifierKind.Trait, trait.Id, 2),
            Assert.Single(swapped.Modifiers));
    }

    [Fact]
    public void A_slot_past_what_the_item_carries_fills_one_position_and_not_the_ones_before_it()
    {
        var stat = Settings.StatCalculation.RandomStatPool[0];

        var filled = ItemCraft.With(
            new TooltipQuery(),
            new CraftSlot(CraftSlots.RandomStat, 1, [CraftSlots.RandomStat]),
            new CraftCandidate(CraftSlots.RandomStat, 0, stat, 1));

        Assert.Equal([stat], filled.RandomStats);

        var trait = Traits[0];

        var carried = ItemCraft.With(
            new TooltipQuery(),
            new CraftSlot(CraftSlots.RandomRolls, 2, [CraftSlots.ItemTrait]),
            new CraftCandidate(CraftSlots.ItemTrait, trait.Id, trait.Name ?? string.Empty, 1));

        Assert.Equal(
            new ItemModifier(ModifierKind.Trait, trait.Id, 1),
            Assert.Single(carried.Modifiers));
    }

    [Fact]
    public void A_roll_the_table_lets_land_on_a_stat_writes_that_stat_and_not_a_modifier()
    {
        var derived = Settings.StatCalculation.RarityAdditions
            .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key)))
            .Select(row => ItemCraft.Slots(
                new ItemDocument { Id = 1, RuleSet = row.Rule, Slot = "Head" },
                Settings,
                Settings.Rarities.Single(rung => rung.Tier == row.Tier)))
            .SelectMany(slots => slots)
            .First(slot =>
                slot.SlotName == CraftSlots.RandomRolls &&
                slot.Accepts.Contains(CraftSlots.RandomStat, StringComparer.Ordinal));

        var stat = Settings.StatCalculation.RandomStatPool[0];
        var candidate = new CraftCandidate(CraftSlots.RandomStat, 0, stat, 1);

        Assert.Equal(CraftSlots.RandomStat, ItemCraft.Writes(derived, candidate));

        var filled = ItemCraft.With(new TooltipQuery(), derived, candidate);

        Assert.Equal([stat], filled.RandomStats);
        Assert.Empty(filled.Modifiers);

        Assert.Null(Forbids(Worn("Head"), new TooltipQuery(), derived, candidate));
    }

    [Fact]
    public void Every_kind_a_derived_roll_accepts_states_where_it_writes()
    {
        foreach (var (ruleSet, byTier) in Settings.StatCalculation.RarityAdditions)
        {
            foreach (var (tier, _) in byTier)
            {
                var slots = ItemCraft.Slots(
                    new ItemDocument { Id = 1, RuleSet = ruleSet, Slot = "Weapon" },
                    Settings,
                    Settings.Rarities.Single(rung => rung.Tier == tier));

                foreach (var slot in slots)
                {
                    foreach (var accepts in slot.Accepts)
                    {
                        var candidate = new CraftCandidate(accepts, 1, "candidate", 1);

                        Assert.Contains(
                            ItemCraft.Writes(slot, candidate),
                            new[]
                            {
                                CraftSlots.RandomStat, CraftSlots.GemSocket,
                                CraftSlots.TraitTree, CraftSlots.RandomRolls,
                            },
                            StringComparer.Ordinal);

                        ItemCraft.With(new TooltipQuery(), slot, candidate);
                    }
                }
            }
        }
    }

    [Fact]
    public void A_socket_slot_stands_only_where_the_rule_set_and_tier_name_one()
    {
        var stated = Settings.StatCalculation.RarityAdditions
            .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key, tier.Value)))
            .Where(row =>
                row.Value.GuaranteedSlots.Contains(CraftSlots.GemSocket, StringComparer.OrdinalIgnoreCase) ||
                row.Value.SpecialSlots.Any(slot =>
                    slot.SlotName.Equals(CraftSlots.GemSocket, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.NotEmpty(stated);

        foreach (var (rule, tier, _) in stated)
        {
            var slots = ItemCraft.Slots(
                new ItemDocument { Id = 1, RuleSet = rule, Slot = "Head" },
                Settings,
                Settings.Rarities.Single(rung => rung.Tier == tier));

            Assert.Contains(slots, slot => slot.SlotName == CraftSlots.GemSocket);
        }

        var socketed = stated[0].Rule;

        var below = Settings.StatCalculation.RarityAdditions[socketed]
            .Where(tier =>
                !tier.Value.GuaranteedSlots.Contains(CraftSlots.GemSocket, StringComparer.OrdinalIgnoreCase) &&
                !tier.Value.SpecialSlots.Any(slot =>
                    slot.SlotName.Equals(CraftSlots.GemSocket, StringComparison.OrdinalIgnoreCase)))
            .Select(tier => tier.Key)
            .ToArray();

        Assert.NotEmpty(below);

        foreach (var tier in below)
        {
            Assert.DoesNotContain(
                ItemCraft.Slots(
                    new ItemDocument { Id = 1, RuleSet = socketed, Slot = "Head" },
                    Settings,
                    Settings.Rarities.Single(rung => rung.Tier == tier)),
                slot => slot.SlotName == CraftSlots.GemSocket);
        }
    }
}
