using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class MaxHealthPolicyTests
{
    private const int StoneSkin = 29;
    private const int ReignInBlood = 2035;
    private const int AnimatedVitality = 3038;

    private const string RuneOfRenewalMaxHealthClass =
        "/Game/Abilities/Heroes/Vigor/Talents/SingleTargetChargedHot_BuffIncreasesMaxHealth/" +
        "GE_Vigor_Talent_SingleTargetChargedHot_BuffIncreasesMaxHealth_Buff." +
        "GE_Vigor_Talent_SingleTargetChargedHot_BuffIncreasesMaxHealth_Buff_C";

    private const string Component = "CRAttributeModComponent";
    private const string ModificationEnum = "EMaxHealthModificationHealthPatchPolicy";
    private const string RemovedEnum = "EMaxHealthModificationHealthReductionPolicy";

    private const string KeepHealthPercentage = "MHMHPP_KeepHealthPercentage";
    private const string HealMaxHealthDelta = "MHMHPP_HealMaxHealthDelta";
    private const string KeepHealthPercentageOnRemoved = "MHMHRP_KeepHealthPercentage";

    private static IEnumerable<(int Id, JsonObject Body)> Effects =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect)
            .Select(document => (document.Id, document.Body));

    private static JsonObject Effect(int id) =>
        Effects.Where(effect => effect.Id == id).Select(effect => effect.Body).FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static string? OnModification(JsonObject effect) => effect["maxHealthModificationPolicy"]?.GetValue<string>();

    private static string? OnRemoved(JsonObject effect) => effect["maxHealthOnRemovedPolicy"]?.GetValue<string>();

    [Theory]
    [InlineData(StoneSkin, KeepHealthPercentage, null)]
    [InlineData(ReignInBlood, HealMaxHealthDelta, null)]
    [InlineData(AnimatedVitality, KeepHealthPercentage, KeepHealthPercentageOnRemoved)]
    public void An_effect_has_the_policies_its_component_states(int id, string onModification, string? onRemoved)
    {
        var effect = Effect(id);

        Assert.Equal(onModification, OnModification(effect));
        Assert.Equal(onRemoved, OnRemoved(effect));
    }

    [Fact]
    public void A_component_at_the_class_default_states_no_policy()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(RuneOfRenewalMaxHealthClass, provider);

        Assert.NotNull(effect);
        Assert.Contains(effect.Owner!.GetExports(), export => export.ExportType == Component);
        Assert.Null(MaxHealthPolicies.Of(effect, provider, provider.MappingsContainer!.MappingsForGame!));
    }

    [Fact]
    public void Every_policy_is_a_member_of_its_enum()
    {
        var mappings = Export.Provider.MappingsContainer!.MappingsForGame!;
        var onModification = EnumOrdinals.Members(mappings, ModificationEnum).Values;
        var onRemoved = EnumOrdinals.Members(mappings, RemovedEnum).Values;

        var modifying = Effects.Select(effect => OnModification(effect.Body)).OfType<string>().ToArray();
        var removing = Effects.Select(effect => OnRemoved(effect.Body)).OfType<string>().ToArray();

        Assert.NotEmpty(modifying);
        Assert.NotEmpty(removing);
        Assert.All(modifying, policy => Assert.Contains(policy, onModification));
        Assert.All(removing, policy => Assert.Contains(policy, onRemoved));
    }
}
