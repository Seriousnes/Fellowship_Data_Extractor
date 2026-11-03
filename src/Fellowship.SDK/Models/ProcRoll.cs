namespace Fellowship.SDK.Models;

/// <summary>
/// A proc-per-minute roll an ability's Blueprint makes on <c>CRRandomStreamComponent.RollProcPerMinute</c>. Dragonbone
/// Dice rolls <c>ArmorSetBonus.PowerBonusOnCrit.PPM</c> times 1 plus the hero's Crit Chance on each critical strike.
/// </summary>
/// <param name="Row">The tuning row the rate is read from.</param>
public sealed record ProcRoll(string Row)
{
    /// <summary>The symbol <see cref="Formula"/> gives the row's value.</summary>
    public const string Rate = "PPM";

    /// <summary>The attribute <see cref="Formula"/> names where <see cref="ScalesWithHaste"/> is set.</summary>
    public const string HasteAttribute = "Haste";

    /// <summary>The row's value.</summary>
    public double? ProcsPerMinute { get; init; }

    /// <summary>
    /// The rate the roll is made at, in <see cref="Rate"/> and the attributes of <see cref="ScalesOff"/>. A percentage
    /// attribute enters as its fraction, so <c>PPM × (1 + CritChance)</c> at 25% Crit Chance is 1.25 times the row.
    /// </summary>
    public required string Formula { get; init; }

    /// <summary>The attributes <see cref="Formula"/> reads, with <see cref="HasteAttribute"/> last where the roll is hasted.</summary>
    public IReadOnlyList<string> ScalesOff { get; init; } = [];

    /// <summary>True when the roll multiplies the rate by 1 plus the hero's Haste percentage.</summary>
    public bool ScalesWithHaste { get; init; }
}
