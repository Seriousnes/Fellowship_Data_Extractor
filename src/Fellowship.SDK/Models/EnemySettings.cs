namespace Fellowship.SDK;

/// <summary>A row of <c>DT_EffectCalculationPresets</c>.</summary>
/// <param name="Flags">Each bool field of <c>CREffectCalculationPreset</c>, by its name.</param>
/// <param name="DefaultSpread">The row's <c>DefaultSpread</c>.</param>
public sealed record EffectCalculationPresetEntry(IReadOnlyDictionary<string, bool> Flags, double DefaultSpread);

/// <summary>
/// The threat values: <c>[/Script/fellowship.CRThreatSettings]</c> in <c>DefaultGame.ini</c>, and the threat values of the
/// ability system globals and the constants tables.
/// </summary>
public sealed record ThreatSettings
{
    public double ThreatRelevancyPercentage { get; init; }

    public double ThreatSurpassPercentageMelee { get; init; }

    public double ThreatSurpassPercentageRange { get; init; }

    public bool ThreatSurpassEnabledForTanks { get; init; }

    public double AggroMeleeRange { get; init; }

    public double InitiationThreatMultiplier { get; init; }

    public double ForcedTargetExtraThreatBuffer { get; init; }

    public double ThreatDecayTickTime { get; init; }

    public double ThreatDecayMultiplierPerTick { get; init; }

    /// <summary><c>CRAbilitySystemGlobals.HealThreatRange</c>.</summary>
    public double HealThreatRange { get; init; }

    /// <summary>The constants row <c>CRAbilitySystemGlobals.GlobalSecondaryThreatMaxDistanceKey</c> names, <c>Global.SecondaryThreatMaxDistance</c>.</summary>
    public double SecondaryThreatMaxDistance { get; init; }

    /// <summary>The constants row <c>Hero.Spirit.Buff.NonTankThreatGenerationScaler</c>.</summary>
    public double NonTankThreatGenerationScaler { get; init; }
}

/// <summary>The AI's global cooldowns and lockouts.</summary>
public sealed record AiGlobals
{
    /// <summary>The constants row <c>Global.DefaultCCCooldown</c>.</summary>
    public double DefaultCCCooldown { get; init; }

    /// <summary>The constants row <c>CRAbilitySystemGlobals.GlobalCCAbilityPadding</c> names, <c>Global.AbilityPadding.CC</c>.</summary>
    public double CCAbilityPadding { get; init; }

    /// <summary>The constants row <c>CRAbilitySystemGlobals.GlobalInterruptAbilityPadding</c> names, <c>Global.AbilityPadding.Interrupt</c>.</summary>
    public double InterruptAbilityPadding { get; init; }

    /// <summary><c>CRAbilitySystemGlobals.DefaultAILockoutDurationForCCInterrupts</c>.</summary>
    public double DefaultAILockoutDurationForCCInterrupts { get; init; }
}

/// <summary>The party health scales of the ability system globals, and the values of <c>GA_DungeonModifier_HolyTrinityNerf</c>.</summary>
public sealed record PartyScaling
{
    /// <summary><c>CRAbilitySystemGlobals.HealthScale4p</c>.</summary>
    public double HealthScale4p { get; init; }

    /// <summary><c>CRAbilitySystemGlobals.HealthScale5p</c>.</summary>
    public double HealthScale5p { get; init; }

    /// <summary>Each scalable float of <c>GA_DungeonModifier_HolyTrinityNerf</c>, by the constants row it reads.</summary>
    public IReadOnlyDictionary<string, double> HolyTrinityNerf { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);
}

/// <summary>
/// One <c>FGameplayEffectAttributeCaptureDefinition</c> of a calculation class's <c>RelevantAttributesToCapture</c>.
/// </summary>
/// <param name="Attribute">The definition's <c>AttributeToCapture</c>.</param>
/// <param name="AttributeSource"><c>Source</c> or <c>Target</c>: whose <see cref="Attribute"/> is captured.</param>
/// <param name="Snapshot">
/// The definition's <c>bSnapshot</c>. With it the capture is a copy of the attribute's aggregator. A source capture is
/// made at the spec's creation, and again before each execution of an effect with
/// <c>RecaptureSourceAttributesBeforeEveryExecution</c>. Without it the capture is the live aggregator, so each
/// execution has the attribute's value of that moment.
/// </param>
public sealed record AttributeCapture(string Attribute, string AttributeSource, bool Snapshot);
