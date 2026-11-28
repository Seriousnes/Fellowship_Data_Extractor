using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class EffectStackTests
{
    private const int EntropicBurst = 2819;
    private const int EntropysClaim = 2603;

    private const string EntropicBurstClass =
        "/Game/Abilities/Heroes/Lisa/Talents/SingleTargetDot_DotAoe/" +
        "GE_Lisa_Talent_SingleTargetDot_DotAoe_Dot.GE_Lisa_Talent_SingleTargetDot_DotAoe_Dot_C";

    private const string EntropysClaimClass =
        "/Game/Abilities/Heroes/Lisa/SingleTargetDot/GE_Lisa_SingleTargetDot_Dot.GE_Lisa_SingleTargetDot_Dot_C";

    private const string StackingBoostTalentDotClass =
        "/Game/Abilities/Heroes/Firemage/InstantSingleTargetDot/" +
        "GE_Firemage_InstantSingleTargetDot_Dot_StackingBoostTalent.GE_Firemage_InstantSingleTargetDot_Dot_StackingBoostTalent_C";

    private const string ChargeCooldownNoHasteClass =
        "/Game/Abilities/Shared/GE_Shared_ChargeCooldownNoHaste.GE_Shared_ChargeCooldownNoHaste_C";

    private const string ChargeEffect = "/Script/fellowship.CRChargeEffect";
    private const string AggregateByTarget = "AggregateByTarget";
    private const string RemoveSingleStackAndRefreshDuration = "RemoveSingleStackAndRefreshDuration";

    private const string TypeField = "StackingType";
    private const string PeriodField = "StackPeriodResetPolicy";
    private const string DurationField = "StackDurationRefreshPolicy";

    private const string TypeEnum = "EGameplayEffectStackingType";
    private const string DurationEnum = "EGameplayEffectStackingDurationPolicy";
    private const string PeriodEnum = "EGameplayEffectStackingPeriodPolicy";
    private const string ExpirationEnum = "EGameplayEffectStackingExpirationPolicy";

    private const string AggregateBySource = "AggregateBySource";
    private const string RefreshOnSuccessfulApplication = "RefreshOnSuccessfulApplication";
    private const string NeverRefresh = "NeverRefresh";
    private const string ResetOnSuccessfulApplication = "ResetOnSuccessfulApplication";
    private const string NeverReset = "NeverReset";
    private const string ClearEntireStack = "ClearEntireStack";
    private const string Instant = "Instant";

    private static readonly string[] StackFields =
    [
        "stackingType", "maxStacks", "stackDurationRefreshPolicy", "stackPeriodResetPolicy", "stackExpirationPolicy",
        "denyOverflowApplication", "clearStackOnOverflow", "overflowEffects",
    ];

    private static IEnumerable<(int Id, JsonObject Body)> Effects =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect)
            .Select(document => (document.Id, document.Body));

    private static JsonObject Effect(int id) =>
        Effects.Where(effect => effect.Id == id).Select(effect => effect.Body).FirstOrDefault()
        ?? throw new InvalidOperationException($"Effect {id} is not in the export. Either the id moved or the effect is gone.");

    private static string? Text(JsonObject effect, string field) => effect[field]?.GetValue<string>();

    private static bool True(JsonObject effect, string field) => effect[field]?.GetValue<bool>() == true;

    [Fact]
    public void Entropic_Burst_has_the_stack_it_states_and_the_policies_the_constructor_stores()
    {
        var effect = Effect(EntropicBurst);

        Assert.Equal(AggregateBySource, Text(effect, "stackingType"));
        Assert.Equal(10, effect["maxStacks"]?.GetValue<int>());
        Assert.Equal(RefreshOnSuccessfulApplication, Text(effect, "stackDurationRefreshPolicy"));
        Assert.Equal(ResetOnSuccessfulApplication, Text(effect, "stackPeriodResetPolicy"));
        Assert.Equal(ClearEntireStack, Text(effect, "stackExpirationPolicy"));
        Assert.Null(effect["silentStack"]);
        Assert.Null(effect["recaptureSourceAttributesBeforeEveryExecution"]);
    }

    [Fact]
    public void An_effect_with_no_stacking_type_has_no_stack_field()
    {
        var effect = Effect(EntropysClaim);

        Assert.All(StackFields, field => Assert.Null(effect[field]));
        Assert.True(True(effect, "recaptureSourceAttributesBeforeEveryExecution"));
    }

    [Fact]
    public void The_reader_has_the_members_of_the_cooked_class()
    {
        var provider = Export.Provider;
        var mappings = provider.MappingsContainer!.MappingsForGame!;
        var burst = ClassDefaults.Load(EntropicBurstClass, provider);
        var claim = ClassDefaults.Load(EntropysClaimClass, provider);

        Assert.NotNull(burst);
        Assert.NotNull(claim);

        var stack = EffectStacks.Of(burst, provider, mappings);

        Assert.NotNull(stack);
        Assert.Equal(AggregateBySource, stack.StackingType);
        Assert.Equal(RefreshOnSuccessfulApplication, stack.DurationRefreshPolicy);
        Assert.Equal(ResetOnSuccessfulApplication, stack.PeriodResetPolicy);
        Assert.Equal(ClearEntireStack, stack.ExpirationPolicy);
        Assert.False(stack.DenyOverflowApplication);
        Assert.False(stack.ClearStackOnOverflow);
        Assert.Empty(stack.OverflowEffects);

        Assert.Null(EffectStacks.Of(claim, provider, mappings));
        Assert.False(EffectStacks.Recaptures(burst, provider));
        Assert.True(EffectStacks.Recaptures(claim, provider));
        Assert.False(EffectStacks.SilentStack(burst, provider));
    }

    [Fact]
    public void A_child_class_has_the_members_its_parent_class_states()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(StackingBoostTalentDotClass, provider);

        Assert.NotNull(effect);
        Assert.DoesNotContain(effect.Properties, property => property.Name.Text is TypeField or PeriodField);
        Assert.Contains(effect.Properties, property => property.Name.Text == DurationField);

        var stack = EffectStacks.Of(effect, provider, provider.MappingsContainer!.MappingsForGame!);

        Assert.NotNull(stack);
        Assert.Equal(AggregateBySource, stack.StackingType);
        Assert.Equal(NeverRefresh, stack.DurationRefreshPolicy);
        Assert.Equal(NeverReset, stack.PeriodResetPolicy);
        Assert.True(EffectStacks.Recaptures(effect, provider));
    }

    [Fact]
    public void A_charge_effect_stating_no_stack_member_has_the_stack_its_native_constructor_stores()
    {
        var provider = Export.Provider;
        var effect = ClassDefaults.Load(ChargeCooldownNoHasteClass, provider);

        Assert.NotNull(effect);
        Assert.Equal(ChargeEffect, ClassDefaults.NativeParent(effect, provider));
        Assert.All(
            ClassDefaults.Chain(effect, provider),
            holder => Assert.DoesNotContain(holder.Properties, property => property.Name.Text == TypeField));

        var stack = EffectStacks.Of(effect, provider, provider.MappingsContainer!.MappingsForGame!);

        Assert.NotNull(stack);
        Assert.Equal(AggregateByTarget, stack.StackingType);
        Assert.Equal(NeverRefresh, stack.DurationRefreshPolicy);
        Assert.Equal(NeverReset, stack.PeriodResetPolicy);
        Assert.Equal(RemoveSingleStackAndRefreshDuration, stack.ExpirationPolicy);
    }

    [Fact]
    public void Each_stacking_effect_has_the_three_policies_and_each_is_a_member_of_its_enum()
    {
        var mappings = Export.Provider.MappingsContainer!.MappingsForGame!;
        var types = EnumOrdinals.Members(mappings, TypeEnum).Values;
        var durations = EnumOrdinals.Members(mappings, DurationEnum).Values;
        var periods = EnumOrdinals.Members(mappings, PeriodEnum).Values;
        var expirations = EnumOrdinals.Members(mappings, ExpirationEnum).Values;

        var stacking = Effects.Where(effect => effect.Body["stackingType"] is not null).ToArray();

        Assert.NotEmpty(stacking);
        Assert.All(stacking, effect =>
        {
            Assert.Contains(Text(effect.Body, "stackingType"), types);
            Assert.Contains(Text(effect.Body, "stackDurationRefreshPolicy"), durations);
            Assert.Contains(Text(effect.Body, "stackPeriodResetPolicy"), periods);
            Assert.Contains(Text(effect.Body, "stackExpirationPolicy"), expirations);
        });

        Assert.Contains(stacking, effect => Text(effect.Body, "stackDurationRefreshPolicy") == NeverRefresh);
        Assert.Contains(stacking, effect => Text(effect.Body, "stackPeriodResetPolicy") == NeverReset);
        Assert.Contains(stacking, effect => Text(effect.Body, "stackExpirationPolicy") != ClearEntireStack);
    }

    [Fact]
    public void The_stack_fields_are_on_stacking_effects_alone()
    {
        var plain = Effects.Where(effect => effect.Body["stackingType"] is null).ToArray();

        Assert.NotEmpty(plain);
        Assert.All(plain, effect => Assert.All(StackFields, field => Assert.Null(effect.Body[field])));
    }

    [Fact]
    public void An_instant_effect_has_no_stack_field()
    {
        var instant = Effects.Where(effect => Text(effect.Body, "durationPolicy") == Instant).ToArray();

        Assert.NotEmpty(instant);
        Assert.All(instant, effect => Assert.All(StackFields, field => Assert.Null(effect.Body[field])));
    }

    [Fact]
    public void A_stack_cap_is_one_or_more()
    {
        var capped = Effects.Where(effect => effect.Body["maxStacks"] is not null).ToArray();
        var open = Effects.Where(effect => effect.Body["stackingType"] is not null && effect.Body["maxStacks"] is null).ToArray();

        Assert.NotEmpty(capped);
        Assert.NotEmpty(open);
        Assert.All(capped, effect => Assert.True(effect.Body["maxStacks"]!.GetValue<int>() >= 1));
        Assert.Contains(capped, effect => effect.Body["maxStacks"]!.GetValue<int>() == 1);
    }

    [Fact]
    public void Overflow_fields_are_on_effects_stating_them()
    {
        var denying = Effects.Where(effect => True(effect.Body, "denyOverflowApplication")).ToArray();
        var clearing = Effects.Where(effect => True(effect.Body, "clearStackOnOverflow")).ToArray();
        var overflowing = Effects.Where(effect => effect.Body["overflowEffects"] is not null).ToArray();

        Assert.NotEmpty(denying);
        Assert.NotEmpty(clearing);
        Assert.NotEmpty(overflowing);
        Assert.All(clearing, effect => Assert.True(True(effect.Body, "denyOverflowApplication")));
        Assert.All(overflowing, effect => Assert.All(
            effect.Body["overflowEffects"]!.AsArray(),
            reference => Assert.Equal("effect", reference!["type"]!.GetValue<string>())));
    }

    [Fact]
    public void Silent_stack_and_recapture_are_true_or_absent()
    {
        var silent = Effects.Where(effect => effect.Body["silentStack"] is not null).ToArray();
        var recapturing = Effects.Where(effect => effect.Body["recaptureSourceAttributesBeforeEveryExecution"] is not null).ToArray();

        Assert.NotEmpty(silent);
        Assert.NotEmpty(recapturing);
        Assert.All(silent, effect => Assert.True(True(effect.Body, "silentStack")));
        Assert.All(recapturing, effect => Assert.True(True(effect.Body, "recaptureSourceAttributesBeforeEveryExecution")));
    }
}
