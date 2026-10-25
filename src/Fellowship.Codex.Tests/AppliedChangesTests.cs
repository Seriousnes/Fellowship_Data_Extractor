using Fellowship.SDK;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Tests;

[Collection(Corpus.Name)]
public class AppliedChangesTests(Export export)
{
    private static readonly EntityReference Target = new(EntityType.Ability, 1, null);

    private static Change Of(string field, string op, double value) => new(Target, field, op, value);

    private IEnumerable<(EntityDocument Source, Change Change)> Changes =>
        export.Store.Entities.SelectMany(document => document switch
        {
            TalentDocument talent => (talent.Changes ?? []).Select(change => ((EntityDocument)talent, change)),
            LegendaryPowerDocument power => (power.Changes ?? []).Select(change => ((EntityDocument)power, change)),
            _ => [],
        });

    [Fact]
    public void Two_multiplicitive_changes_add_their_excess_over_one()
    {
        var scale = AppliedChanges.Aggregated(1, [Of("DamageScale", "Multiplicitive", 1.2), Of("DamageScale", "Multiplicitive", 1.2)]);

        Assert.Equal(1.4, scale, 10);
    }

    [Fact]
    public void Two_compound_changes_multiply()
    {
        var scale = AppliedChanges.Aggregated(1, [Of("DamageScale", "MultiplyCompound", 1.4), Of("DamageScale", "MultiplyCompound", 1.4)]);

        Assert.Equal(1.96, scale, 10);
    }

    [Fact]
    public void An_override_replaces_every_other_change()
    {
        var scale = AppliedChanges.Aggregated(1, [Of("CritChance", "Additive", 0.2), Of("CritChance", "Override", 1)]);

        Assert.Equal(1, scale, 10);
    }

    [Fact]
    public void A_cooldown_multiplier_scales_the_pre_additive_and_not_the_post_additive()
    {
        var cooldown = AppliedChanges.Cooled(
            10,
            [
                Of("cooldown", "CooldownPreAdditives", -2),
                Of("cooldown", "CooldownMultipliers", 0.5),
                Of("cooldown", "CooldownPostAdditives", -1),
            ]);

        Assert.Equal(3, cooldown, 10);
    }

    [Fact]
    public void A_set_wins_over_an_add_on_the_same_field()
    {
        var cast = AppliedChanges.Stepped(2, [Of("castTime", "Add", 0.5), Of("castTime", "Set", 0)]);

        Assert.Equal(0, cast, 10);
    }

    [Fact]
    public void Every_change_names_a_target_whose_changed_by_lists_its_source()
    {
        foreach (var (source, change) in Changes)
        {
            var target = Assert.Single(export.Store.All(change.Target.Type, change.Target.Id).Take(1));

            Assert.Contains(
                DocumentFieldsOf(target),
                listed => listed.Type == EntityTypes.TypeOf(source) && listed.Id == source.Id);
        }
    }

    [Fact]
    public void Ticking_a_source_with_a_cooldown_multiplier_multiplies_the_target_cooldown()
    {
        var cases = Changes
            .Where(entry => entry.Change is { Op: "CooldownMultipliers", While: null })
            .Select(entry => (entry.Source, entry.Change, Target: export.Store.All(entry.Change.Target.Type, entry.Change.Target.Id).FirstOrDefault()))
            .Where(entry => entry.Target is AbilityDocument { Cooldown: not null })
            .Where(entry => AppliedChanges.Toward(entry.Source, entry.Target!).Count(change => change.Field == "cooldown") == 1)
            .ToArray();

        Assert.NotEmpty(cases);

        foreach (var (source, change, target) in cases)
        {
            var ticked = new ChangeSource(EntityTypes.TypeOf(source), source.Id);
            var changed = Assert.IsType<AbilityDocument>(AppliedChanges.Apply(target!, [ticked], export.Store));

            Assert.Equal(((AbilityDocument)target!).Cooldown!.Value * change.Value, changed.Cooldown!.Value, 10);
        }
    }

    [Fact]
    public void Ticking_a_source_whose_only_change_is_a_crit_chance_leaves_the_coefficients()
    {
        var cases = Changes
            .Where(entry => entry.Change is { Field: "CritChance", While: null })
            .Select(entry => (entry.Source, Target: export.Store.All(entry.Change.Target.Type, entry.Change.Target.Id).FirstOrDefault()))
            .Where(entry => entry.Target is AbilityDocument { Placeholders.Count: > 0 })
            .Where(entry => AppliedChanges.Toward(entry.Source, entry.Target!).All(change => change.Field == "CritChance"))
            .ToArray();

        Assert.NotEmpty(cases);

        foreach (var (source, target) in cases)
        {
            var ticked = new ChangeSource(EntityTypes.TypeOf(source), source.Id);
            var changed = Assert.IsType<AbilityDocument>(AppliedChanges.Apply(target!, [ticked], export.Store));

            Assert.Equal(
                ((AbilityDocument)target!).Placeholders!.Select(placeholder => placeholder.Coefficient),
                changed.Placeholders!.Select(placeholder => placeholder.Coefficient));
        }
    }

    private static IReadOnlyList<EntityReference> DocumentFieldsOf(EntityDocument target) => target switch
    {
        AbilityDocument ability => ability.ChangedBy ?? [],
        EffectDocument effect => effect.ChangedBy ?? [],
        _ => [],
    };
}
