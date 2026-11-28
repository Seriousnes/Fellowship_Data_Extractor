using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class AttributeCaptureTests
{
    private const int EntropicBurst = 2819;

    private const string Source = "Source";
    private const string Target = "Target";

    private const string DamageBase = "CRDamageCalculationBase";
    private const string HealBase = "CRHealCalculationBase";
    private const string IntellectDamage = "CRIntellectDamageCalculation";
    private const string ManaGain = "CRManaGainCalculation";

    private const string StrengthDamage = "CRStrengthDamageCalculation";

    private const string CleaveDamageClass =
        "/Game/Abilities/Heroes/Ink/CleaveAttack/GE_Ink_CleaveAttack_Damage.GE_Ink_CleaveAttack_Damage_C";

    private static IReadOnlyDictionary<string, IReadOnlyList<AttributeCapture>> Captures =>
        Export.Loaded.Settings.RelevantAttributesToCapture;

    private static IEnumerable<(int Id, JsonObject Body)> Effects =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect)
            .Select(document => (document.Id, document.Body));

    [Fact]
    public void The_Intellect_damage_calculation_captures_its_source_attributes_as_snapshots()
    {
        var captures = Captures[IntellectDamage];

        Assert.All(
            new[] { "Intellect", "DamageScale", "Expertise", "BalancingDamageAura", "CritChance", "CritMultiplier" },
            attribute => Assert.Contains(new AttributeCapture(attribute, Source, true), captures));
        Assert.All(
            new[] { "IncomingDamageMultiplier", "MagicDamageReduction", "MagicalIncomingDamageMultiplier", "DamageMitigationScale" },
            attribute => Assert.Contains(new AttributeCapture(attribute, Target, false), captures));
        Assert.Equal(25, captures.Count);
    }

    [Fact]
    public void Every_source_definition_is_a_snapshot_and_every_target_definition_is_live()
    {
        var all = Captures.Values.SelectMany(captures => captures).ToArray();

        Assert.NotEmpty(all);
        Assert.All(all, capture => Assert.Equal(capture.AttributeSource == Source, capture.Snapshot));
        Assert.All(all, capture => Assert.Contains(capture.AttributeSource, new[] { Source, Target }));
    }

    [Fact]
    public void A_main_stat_class_has_its_base_class_definitions_and_one_more()
    {
        var damage = Captures[DamageBase];
        var heal = Captures[HealBase];

        Assert.Equal(24, damage.Count);
        Assert.Equal(12, heal.Count);

        foreach (var stat in new[] { "Strength", "Agility", "Intellect", "PrimaryAttribute" })
        {
            var own = new AttributeCapture(stat, Source, true);

            Assert.Equal([.. damage, own], Captures[$"CR{stat}DamageCalculation"]);
            Assert.Equal([.. heal, own], Captures[$"CR{stat}HealCalculation"]);
        }

        Assert.Equal(damage, Captures["CRUserDefinedDamageCalculation"]);
        Assert.Equal(heal, Captures["CRUserDefinedHealCalculation"]);
        Assert.Empty(Captures[ManaGain]);
    }

    [Fact]
    public void Every_modifier_on_a_captured_attribute_names_a_definition_of_its_calculation_class()
    {
        var checkedModifiers = 0;

        foreach (var (id, effect) in Effects)
        {
            foreach (var field in new[] { "setByCallerModifiers", "calculationModifiers" })
            {
                if (effect[field] is not JsonArray modifiers) continue;

                foreach (var modifier in modifiers.OfType<JsonObject>())
                {
                    if (modifier["attribute"]?.GetValue<string>() is not { } attribute) continue;

                    var calculation = modifier["execution"]!.GetValue<string>();

                    Assert.True(Captures.ContainsKey(calculation), $"Effect {id} has a modifier on {attribute} of {calculation}.");

                    var definitions = Captures[calculation].Where(capture => capture.Attribute == attribute).ToArray();

                    Assert.True(definitions.Length > 0, $"Effect {id}: {calculation} has no definition of {attribute}.");

                    if (modifier["attributeSource"]?.GetValue<string>() is { } source)
                    {
                        Assert.Contains(definitions, capture => capture.AttributeSource == source);
                    }

                    checkedModifiers++;
                }
            }
        }

        Assert.True(checkedModifiers > 100, $"{checkedModifiers} modifiers on a captured attribute were checked.");
    }

    [Fact]
    public void Entropic_Burst_has_the_source_values_of_its_spec_for_every_tick()
    {
        var effect = Effects.Where(candidate => candidate.Id == EntropicBurst).Select(candidate => candidate.Body).Single();
        var calculation = effect["executions"]!.AsArray().Single()!.GetValue<string>();
        var scaling = effect["scalesOff"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

        Assert.Equal(IntellectDamage, calculation);
        Assert.Null(effect["recaptureSourceAttributesBeforeEveryExecution"]);
        Assert.All(scaling, attribute => Assert.Contains(new AttributeCapture(attribute, Source, true), Captures[calculation]));
    }

    [Fact]
    public void A_cooked_scoped_modifier_states_the_definition_the_constructor_adds()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(CleaveDamageClass, provider);

        Assert.NotNull(effect);

        // The effect has scoped modifiers on Strength, a source snapshot, and on DodgeChance, a live target capture.
        // The read checks each CapturedAttribute against the class's definitions and throws on a difference.
        CalculationModifiers.Of(effect, provider, new ConstantsTable(provider));

        CalculationCaptures.Check(StrengthDamage, "DodgeChance", Target, false, effect.Name, provider);
        CalculationCaptures.Check(StrengthDamage, "Strength", Source, true, effect.Name, provider);

        Assert.Throws<InvalidOperationException>(
            () => CalculationCaptures.Check(StrengthDamage, "DodgeChance", Target, true, effect.Name, provider));
        Assert.Throws<InvalidOperationException>(
            () => CalculationCaptures.Check(StrengthDamage, "Strength", Source, false, effect.Name, provider));
        Assert.Throws<InvalidOperationException>(
            () => CalculationCaptures.Check(StrengthDamage, "Intellect", Source, true, effect.Name, provider));
        Assert.Throws<InvalidOperationException>(
            () => CalculationCaptures.Check("CRModifyStaggerExecution", "Strength", Source, true, effect.Name, provider));
    }
}
