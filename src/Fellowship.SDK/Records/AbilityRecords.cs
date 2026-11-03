using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public sealed record AbilityRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }


    public string? Description { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }

    public AbilityCategory? Category { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? GlobalCooldown { get; init; }
    public bool? GlobalCooldownScalesWithHaste { get; init; }
    public double? Charges { get; init; }
    public IReadOnlyDictionary<string, double>? ChargesByLevelType { get; init; }
    public IReadOnlyList<TargetCountFalloffRecord>? TargetCountFalloff { get; init; }
    public double? MaxTargets { get; init; }
    public IReadOnlyList<string>? CooldownDurationScalesOff { get; init; }
    public IReadOnlyList<string>? CooldownTimeRateScalesOff { get; init; }
    public IReadOnlyList<RecordReference>? CooldownHasteGrantedBy { get; init; }
    public IReadOnlyList<RecordReference>? CooldownAccelerationGrantedBy { get; init; }
    public double? Cost { get; init; }

    public string? Resource { get; init; }

    public double? Range { get; init; }
    public double? MinRange { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    public bool? CastTimeScalesWithHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<ProcRollRecord>? ProcRolls { get; init; }
    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public RecordReference? Owner { get; init; }

    public AbilityDifficultyRecord? Difficulty { get; init; }
}

/// <summary>
/// A proc-per-minute roll with its rate in the attributes' display names.
/// </summary>
/// <param name="Row">The tuning row the rate is read from.</param>
/// <param name="Formula">The rate, in <c>PPM</c> for the row's value and the display names of <see cref="ScalesOff"/>.</param>
public sealed record ProcRollRecord(string Row, string Formula)
{
    public double? ProcsPerMinute { get; init; }

    public IReadOnlyList<string> ScalesOff { get; init; } = [];

    public bool ScalesWithHaste { get; init; }

    /// <summary>The talent or legendary power whose tag the roll is behind. A roll with none is baseline.</summary>
    public RecordReference? Talent { get; init; }
}

public sealed record AbilityDifficultyRecord(int Difficulty, string Label)
{
    public IReadOnlyList<DamageBandRecord> Damage { get; init; } = [];
}

public sealed record DamageBandRecord(decimal Mean, decimal Low, decimal High)
{
    public string? Part { get; init; }
}
