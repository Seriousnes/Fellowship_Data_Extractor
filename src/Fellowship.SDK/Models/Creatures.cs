namespace Fellowship.SDK.Models;

public enum CreatureRank
{
    Enemy,
    Elite,
    Boss,
    Friendly,
}

public sealed record CreatureAbility(EntityReference Ability, string Phase, bool Shown)
{
    /// <summary>The slot's <c>Priority</c>.</summary>
    public int? Priority { get; init; }

    /// <summary>The slot's <c>ActivationType</c>, a member of <c>ECRPhaseAbilityActivationType</c>.</summary>
    public string? Activation { get; init; }

    /// <summary>The slot's <c>DifficultyUnlocked</c>: the lowest difficulty the slot activates at.</summary>
    public double? DifficultyUnlocked { get; init; }

    /// <summary>The slot's <c>IsAbilityInterruptable</c>.</summary>
    public bool? Interruptible { get; init; }

    /// <summary>The slot's <c>ActivationTag</c>.</summary>
    public string? ActivationTag { get; init; }

    public bool? EnabledByDefault { get; init; }

    /// <summary>The slot's <c>PreserveCooldownBetweenPhases</c>.</summary>
    public bool? PreserveCooldownBetweenPhases { get; init; }

    public PhaseCooldown? InitialCooldown { get; init; }

    public PhaseCooldown? Cooldown { get; init; }

    public PhaseCooldown? InterruptCooldown { get; init; }

    public PhaseCooldown? CCCooldown { get; init; }

    public PhaseCooldown? PostAbilityPadding { get; init; }
}

public sealed record PhaseCooldown(string Type)
{
    public double? Seconds { get; init; }

    public double? MinSeconds { get; init; }

    public double? MaxSeconds { get; init; }

    /// <summary>The <c>CooldownCalculationClass</c> of a <c>CustomCalculation</c> cooldown.</summary>
    public string? Calculation { get; init; }
}

/// <summary>A phase of a creature's behaviour component, by its <c>AIPhase.*</c> tag.</summary>
/// <param name="Phase">The phase's name, as each of its abilities' <see cref="CreatureAbility.Phase"/> has it.</param>
/// <param name="Tag">The phase's gameplay tag. A phase keyed by no tag has none.</param>
/// <param name="Type"><c>Unique</c>, or the name of the generic phase asset a <c>Generic</c> phase reads.</param>
public sealed record CreaturePhase(string Phase, string? Tag, string Type);

public sealed record AbilityCoefficient(string Attribute, double Value)
{
    public string? Part { get; init; }

    public double? Spread { get; init; }
}
