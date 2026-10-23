namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class CraftRuleTests(Export export)
{
    private SettingsFile Settings => export.Store.Settings;

    private IReadOnlyList<GlyphDocument> GlyphDocuments =>
        [.. export.Store.Entities.OfType<GlyphDocument>()];

    private string Defined(string slotType) =>
        Settings.StatCalculation.SlotDefinitions.First(pair => pair.Value.SlotType == slotType).Key;

    private string Glyphs => Defined(CraftSlots.AbilityRank);

    private string Augmented => Settings.StatCalculation.SlotDefinitions
        .Where(pair => pair.Value.SlotType == CraftSlots.Augmentation)
        .MaxBy(pair => pair.Value.MinimumItemLevel)
        .Key;

    private IReadOnlyList<AugmentationDocument> Pooled =>
    [
        .. Settings.StatCalculation.SlotDefinitions[Augmented].AugmentationPool
            .SelectMany(category => AttunementTrees.InCategory(category, export.Store))
            .Where(augmentation => augmentation.MaxRank is > 0),
    ];

    private TooltipQuery Top => new() { ItemLevel = Settings.Items.MaxItemLevel };

    private static CraftSlot Roll(params string[] accepts) => new(CraftSlots.RandomRolls, 0, accepts);

    private static ItemDocument Worn(string slot) => new() { Id = 1, Slot = slot };

    private string? Forbids(ItemDocument item, TooltipQuery query, CraftSlot slot, CraftCandidate candidate) =>
        ItemCraft.Forbids(item, query, slot, candidate, export.Store);

    private static CraftCandidate Candidate(string accepts, EntityDocument row, int rank = 1) =>
        new(accepts, row.Id, row.Name ?? string.Empty, rank);

    private string Warning(string slot) => Settings.ModifierTypes.Single(type => type.Name == slot).MaximumLimitWarning!;

    [Fact]
    public void A_modifier_past_its_type_cap_is_forbidden_with_the_type_warning()
    {
        foreach (var type in Settings.ModifierTypes)
        {
            Assert.NotNull(type.MaximumLimit);
            Assert.NotNull(type.MaximumLimitWarning);

            var kind = ItemCraft.KindOf(type.Name, Settings)!.Value;
            var row = TooltipView.TypeOf(kind, Settings) switch
            {
                var augmented when AugmentationTypes.Is(augmented) => (EntityDocument)Settings.StatCalculation.SlotDefinitions[type.Name].AugmentationPool
                    .SelectMany(category => AttunementTrees.InCategory(category, export.Store))
                    .First(augmentation => augmentation.MaxRank is > 0),
                EntityType.Glyph => GlyphDocuments.First(glyph => glyph.Slots is { Count: > 0 }),
                var entity => export.Store.Entities.First(document => EntityTypes.TypeOf(document) == entity),
            };

            var full = Enumerable.Range(0, type.MaximumLimit!.Value)
                .Select(at => new ItemModifier(kind, -1 - at, 1))
                .ToArray();

            var worn = row is GlyphDocument { Slots: [var slot, ..] } ? slot : "Head";

            Assert.Equal(
                type.MaximumLimitWarning,
                Forbids(Worn(worn), Top with { Modifiers = full }, Roll(type.Name), Candidate(type.Name, row)));

            Assert.Null(Forbids(Worn(worn), Top with { Modifiers = full[1..] }, Roll(type.Name), Candidate(type.Name, row)));
        }
    }

    [Fact]
    public void A_modifier_below_the_item_level_its_slot_starts_at_is_forbidden()
    {
        var floor = Settings.StatCalculation.SlotDefinitions[Augmented].MinimumItemLevel;
        var augmentation = Pooled[0];

        Assert.True(floor > 0);

        Assert.Equal(
            CraftReasons.BelowItemLevel(floor),
            Forbids(Worn("Head"), Top with { ItemLevel = floor - 1 }, Roll(Augmented), Candidate(Augmented, augmentation)));

        Assert.Null(Forbids(Worn("Head"), Top with { ItemLevel = floor }, Roll(Augmented), Candidate(Augmented, augmentation)));
    }

    [Fact]
    public void A_third_instance_of_one_augmentation_id_is_forbidden()
    {
        var augmentation = Pooled[0];
        var already = new ItemModifier(ItemCraft.KindOf(Augmented, Settings)!.Value, augmentation.Id, 1);

        Assert.Null(Forbids(
            Worn("Head"), Top with { Modifiers = [already] }, Roll(Augmented), Candidate(Augmented, augmentation)));

        Assert.Equal(
            Warning(Augmented),
            Forbids(Worn("Head"), Top with { Modifiers = [already, already] }, Roll(Augmented), Candidate(Augmented, augmentation)));
    }

    [Fact]
    public void A_third_glyph_is_forbidden_at_every_tier()
    {
        var offered = GlyphDocuments.Where(glyph => glyph.Slots is { Count: > 0 }).Take(3).ToArray();

        Assert.Equal(3, offered.Length);

        var carried = offered
            .Take(2)
            .Select(glyph => new ItemModifier(ItemCraft.KindOf(Glyphs, Settings)!.Value, glyph.Id, 1))
            .ToArray();

        Assert.Equal(
            Warning(Glyphs),
            Forbids(
                Worn(offered[2].Slots![0]),
                Top with { Modifiers = carried },
                Roll(Glyphs),
                Candidate(Glyphs, offered[2])));
    }

    [Fact]
    public void A_glyph_offers_only_on_a_slot_its_pools_name()
    {
        var glyph = GlyphDocuments.First(row => row.Slots is { Count: > 0 });

        Assert.All(glyph.Slots!, worn =>
            Assert.Null(Forbids(Worn(worn), Top, Roll(Glyphs), Candidate(Glyphs, glyph))));

        const string Elsewhere = "Offhand";

        Assert.DoesNotContain(Elsewhere, glyph.Slots!, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            CraftReasons.NotInPool(Elsewhere),
            Forbids(Worn(Elsewhere), Top, Roll(Glyphs), Candidate(Glyphs, glyph)));
    }

    [Fact]
    public void A_glyph_runs_from_one_to_the_rank_its_row_states()
    {
        var glyph = GlyphDocuments.First(row => row.Slots is { Count: > 0 } && row.MaxRank is > 0);
        var worn = glyph.Slots![0];

        string? At(int rank) => Forbids(Worn(worn), Top, Roll(Glyphs), Candidate(Glyphs, glyph, rank));

        Assert.Null(At(1));
        Assert.Null(At(glyph.MaxRank!.Value));
        Assert.Equal(CraftReasons.PastRank(glyph.MaxRank.Value), At(glyph.MaxRank.Value + 1));
        Assert.Equal(CraftReasons.PastRank(glyph.MaxRank.Value), At(0));
    }

    [Fact]
    public void A_augmentation_runs_from_one_to_the_rank_its_document_states()
    {
        var augmentation = Pooled[0];

        string? At(int rank) => Forbids(Worn("Head"), Top, Roll(Augmented), Candidate(Augmented, augmentation, rank));

        Assert.Null(At(augmentation.MaxRank!.Value));
        Assert.Equal(CraftReasons.PastRank(augmentation.MaxRank.Value), At(augmentation.MaxRank.Value + 1));
    }

    [Fact]
    public void An_attunement_sits_once_and_only_while_a_slot_is_free()
    {
        var tree = Settings.AttunementTrees[0];
        var capacity = AttunementTrees.Capacity(tree.Tag, Settings)!.Value;
        var grown = Defined(CraftSlots.AttunementTree);
        var item = new ItemDocument { Id = 1, Slot = "Weapon", AttunementTree = tree.Tag };
        var block = new CraftSlot(CraftSlots.AttunementTree, 0, [grown]) { Capacity = capacity };

        var chosen = tree.Nodes
            .SelectMany(node => AttunementTrees.Offered(node, export.Store))
            .DistinctBy(augmentation => augmentation.Id)
            .Where(augmentation => augmentation.MaxRank is > 0)
            .Take(capacity + 1)
            .ToArray();

        Assert.Equal(capacity + 1, chosen.Length);

        var filled = chosen.Take(capacity).Select(row => new Attunement(row.Id, 1)).ToArray();

        var named = TooltipView.TreeName(Settings);

        Assert.Equal(
            CraftReasons.AlreadyAttuned(named),
            Forbids(item, Top with { Attunements = [filled[0]] }, block, Candidate(grown, chosen[0])));

        Assert.Equal(
            CraftReasons.NoRoom(named),
            Forbids(item, Top with { Attunements = filled }, block, Candidate(grown, chosen[^1])));
    }

    [Fact]
    public void A_slot_does_not_count_what_it_already_holds_against_the_choice_replacing_it()
    {
        var offered = GlyphDocuments.Where(glyph => glyph.Slots is { Count: > 0 }).Take(2).ToArray();
        var worn = offered[0].Slots![0];

        var carried = offered
            .Select(glyph => new ItemModifier(ItemCraft.KindOf(Glyphs, Settings)!.Value, glyph.Id, 1))
            .ToArray();

        var query = Top with { Modifiers = carried };
        var slot = Roll(Glyphs);
        var candidate = Candidate(Glyphs, offered[0]);

        Assert.Equal(Warning(Glyphs), Forbids(Worn(worn), query, slot, candidate));

        Assert.Null(Forbids(Worn(worn), ItemCraft.Without(query, slot), slot, candidate));
    }

    [Fact]
    public void A_slot_the_candidate_fills_states_the_query_it_would_become()
    {
        var augmentation = Pooled[0];

        var swapped = ItemCraft.With(
            new TooltipQuery { Modifiers = [new ItemModifier(ItemCraft.KindOf(Glyphs, Settings)!.Value, 1, 1)] },
            Roll(Augmented),
            Candidate(Augmented, augmentation, 2),
            Settings);

        Assert.Equal(new ItemModifier(ItemCraft.KindOf(Augmented, Settings)!.Value, augmentation.Id, 2), Assert.Single(swapped.Modifiers));
    }

    [Fact]
    public void A_slot_past_what_the_item_carries_fills_one_position_and_not_the_ones_before_it()
    {
        var augmentation = Pooled[0];

        var carried = ItemCraft.With(
            new TooltipQuery(),
            new CraftSlot(CraftSlots.RandomRolls, 2, [Augmented]),
            Candidate(Augmented, augmentation),
            Settings);

        Assert.Equal(new ItemModifier(ItemCraft.KindOf(Augmented, Settings)!.Value, augmentation.Id, 1), Assert.Single(carried.Modifiers));
    }

    [Fact]
    public void Each_guaranteed_slot_becomes_the_slot_type_its_definition_names()
    {
        var rows = Settings.StatCalculation.RarityAdditions
            .SelectMany(rule => rule.Value.Select(tier => (Rule: rule.Key, Tier: tier.Key, Added: tier.Value)))
            .ToArray();

        Assert.Contains(rows, row => row.Added.GuaranteedSlots.Any(name =>
            ItemCraft.Definition(name, Settings).SlotType == CraftSlots.AttunementTree));

        foreach (var (rule, tier, added) in rows)
        {
            var slots = ItemCraft.Slots(
                new ItemDocument { Id = 1, RuleSet = rule },
                Settings,
                Settings.Rarities.Single(rung => rung.Tier == tier));

            var types = added.GuaranteedSlots
                .Select(name => ItemCraft.Definition(name, Settings).SlotType)
                .ToArray();

            Assert.Equal(types, slots.Take(types.Length).Select(slot => slot.SlotName));

            Assert.All(
                slots.Where(slot => slot.SlotName is CraftSlots.AttunementTree or CraftSlots.GemSocket),
                slot => Assert.Contains(Assert.Single(slot.Accepts), added.GuaranteedSlots));
        }
    }
}
