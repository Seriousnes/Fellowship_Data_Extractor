namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class SortingTests(Export export)
{
    private ICodexService Codex => export.View.Codex;

    [Fact]
    public void Every_sorting_key_is_spelled_once()
    {
        var keys = Codex.Sortings.Select(sorting => sorting.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void A_sorting_that_shadows_a_measure_orders_everything_the_measure_ordered()
    {
        foreach (var sorting in Codex.Sortings)
        {
            if (Codex.Measure(sorting.Key) is not { } measure) continue;

            var lost = export.Store.Entities
                .Where(row => measure.Value(row) is not null && sorting.Value(row) is null)
                .ToArray();

            Assert.True(lost.Length == 0,
                $"{sorting.Key} shadows the {measure.Key} measure and answers nothing for " +
                $"{lost.Length} rows the measure ordered, the first being {PublicTypes.NameOf(lost.FirstOrDefault() ?? export.Store.Entities[0])}.");
        }
    }

    [Fact]
    public void Every_sorting_is_a_sort_the_query_accepts()
    {
        foreach (var sorting in Codex.Sortings)
        {
            Searching.Ask(export, out var error, ("sort", sorting.Key));

            Assert.Null(error);
        }
    }

    [Fact]
    public void Sorting_by_item_level_the_other_way_opens_at_the_ceiling_and_ends_in_nothing()
    {
        var results = Searching.Run(export, ("type", "item"), ("sort", "ilvl"), ("dir", "desc"));

        var levels = results.Rows.Select(row => ((ItemDocument)row).ItemLevel).ToArray();

        Assert.Equal(360, levels[0]);
        Assert.Equal(levels.OrderBy(level => level is null).ThenByDescending(level => level), levels);
    }

    [Fact]
    public void Sorting_by_item_level_keeps_the_rows_that_carry_none_last()
    {
        var query = Searching.Ask(export, out _, ("type", "item"), ("sort", "ilvl"));
        var results = Searching.Run(export, query with { PageSize = CodexQuery.MaximumPageSize });

        var levels = results.Rows.Select(row => ((ItemDocument)row).ItemLevel).ToArray();

        Assert.Equal(levels.OrderBy(level => level is null).ThenBy(level => level), levels);
    }

    [Fact]
    public void Sorting_talents_by_tier_reads_the_slot_each_one_sits_in()
    {
        var results = Searching.Run(export, ("type", "talent"), ("sort", "tier"));

        var tiers = results.Rows.Select(row => export.Store.Slot(row.Id)?.Row).ToArray();

        Assert.NotEmpty(tiers);
        Assert.Contains(tiers, tier => tier is not null);
        Assert.Equal(tiers.OrderBy(tier => tier is null).ThenBy(tier => tier), tiers);
    }

    [Fact]
    public void Sorting_talents_by_cost_reads_the_slot_rather_than_the_talents_own_cost()
    {
        var cost = Codex.Sorting("cost")!;
        var talent = export.Store.Entities.OfType<TalentDocument>().First(row => export.Store.Slot(row.Id) is not null);

        Assert.Equal((double)export.Store.Slot(talent.Id)!.Cost, cost.Value(talent));
    }

    [Fact]
    public void Sorting_effects_by_applier_reads_the_ability_the_column_names()
    {
        var applier = Codex.Sorting("applier")!;

        var effect = export.Store.Entities.OfType<EffectDocument>().First(row => row.AppliedBy is not null);
        var ability = export.Store.All(effect.AppliedBy!.Type, effect.AppliedBy.Id)[0];

        Assert.Equal(PublicTypes.NameOf(ability), applier.Value(effect));
    }

    [Fact]
    public void Sorting_effects_by_applier_keeps_the_ones_no_single_ability_applies_last()
    {
        var applier = Codex.Sorting("applier")!;

        var query = Searching.Ask(export, out _, ("type", "effect"), ("sort", "applier"));
        var results = Searching.Run(export, query with { PageSize = CodexQuery.MaximumPageSize });

        var named = results.Rows.Select(row => applier.Value(row)).ToArray();

        Assert.Contains(named, name => name is not null);
        Assert.Equal(named.OrderBy(name => name is null), named.AsEnumerable());
    }

    [Fact]
    public void An_unknown_sort_key_answers_a_problem_naming_the_legal_keys()
    {
        Searching.Ask(export, out var error, ("sort", "nonsense"));

        Assert.NotNull(error);
        Assert.Contains("ilvl", error, StringComparison.Ordinal);
        Assert.Contains("cooldown", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_measure_is_still_a_sort_the_query_accepts()
    {
        var results = Searching.Run(export, ("type", "ability"), ("sort", "cooldown"), ("dir", "desc"));

        var cooldowns = results.Rows.Select(row => ((AbilityDocument)row).Cooldown).ToArray();

        Assert.Equal(cooldowns.OrderBy(value => value is null).ThenByDescending(value => value), cooldowns);
    }

    [Fact]
    public void Sorting_items_by_type_reads_the_slot_before_the_armour_it_carries()
    {
        var kind = Codex.Sorting("kind")!;

        var rings = export.Store.Entities.OfType<ItemDocument>()
            .Where(item => item.Slot == "Ring" && item.ArmorType is not null)
            .ToArray();

        Assert.NotEmpty(rings);
        Assert.All(rings, ring => Assert.Equal("Ring", kind.Value(ring)));

        var armour = export.Store.Entities.OfType<ItemDocument>().First(item => item.Slot == "Chest");

        Assert.Equal(armour.ArmorType?.ToString(), kind.Value(armour));
    }
}
