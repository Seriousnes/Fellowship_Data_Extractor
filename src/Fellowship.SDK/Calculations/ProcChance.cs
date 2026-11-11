using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.SDK.Calculations;

/// <summary>
/// A proc a player picks by the thing they hold or cast, with the rolls behind it.
/// </summary>
/// <param name="Face">The item, talent, legendary power or ability the proc is picked and shown as.</param>
/// <param name="Carrier">The document carrying the rolls: a relic passive behind its relic, otherwise the face itself.</param>
/// <param name="Rolls">The rolls the carrier makes in its own right.</param>
public sealed record ProcRollSource(EntityDocument Face, EntityDocument Carrier, IReadOnlyList<ProcRoll> Rolls)
{
    public EntityType Type => EntityTypes.TypeOf(Face);

    public string Key => EntityTypes.Slug(Type);

    /// <summary>Other documents carrying the same rolls for the face: an ability the augmentation grants.</summary>
    public IReadOnlyList<EntityDocument> Others { get; init; } = [];

    public bool Is(EntityType type, int id) =>
        (Type == type && Face.Id == id) ||
        Others.Prepend(Carrier).Any(carrier => EntityTypes.TypeOf(carrier) == type && carrier.Id == id);
}

/// <summary>One roll's chance at a rate: the base, the drought factor, the first-proc floor and the result.</summary>
/// <param name="Rate">Procs per minute after every attribute in the formula.</param>
/// <param name="Base">Rate × seconds since the last roll ÷ 60.</param>
/// <param name="Drought">max(1, 3 × Rate × seconds since the last proc ÷ 60 − 3.5).</param>
/// <param name="Floor">Rate ÷ the first-proc divisor, applied until the stream's first proc.</param>
/// <param name="Chance">The chance to proc, between 0 and 1.</param>
public sealed record ProcChanceRoll(double Rate, double Base, double Drought, double? Floor, double Chance);

public static class ProcChance
{
    private const double SecondsPerMinute = 60;
    private const double DroughtSlope = 3;
    private const double DroughtOffset = 3.5;
    private const double LeastDivisor = 0.1;
    private const string Times = " × (1 + ";
    private const string Close = ")";
    private const double DefaultSinceRoll = 1;
    private const double DefaultSinceProc = 30;

    /// <summary>
    /// The seconds since the last roll and the last proc a roll starts at. The Blueprint makes no roll during its
    /// internal cooldown, so both reach the cooldown before the next roll can happen.
    /// </summary>
    public static (double SinceRoll, double SinceProc) Starting(ProcRoll roll) =>
        roll.InternalCooldown is { } cooldown ? (cooldown, cooldown) : (DefaultSinceRoll, DefaultSinceProc);

    public static IReadOnlyList<ProcRoll>? Rolls(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.ProcRolls,
        RelicPassiveDocument relic => relic.ProcRolls,
        TalentDocument talent => talent.ProcRolls,
        LegendaryPowerDocument legendary => legendary.ProcRolls,
        GemAbilityDocument gem => gem.ProcRolls,
        DungeonAffixDocument affix => affix.ProcRolls,
        WeaponPowerDocument weapon => weapon.ProcRolls,
        RelicPowerDocument power => power.ProcRolls,
        AugmentationDocument augmentation => augmentation.ProcRolls,
        _ => null,
    };

    /// <summary>The rolls the document makes in its own right: an ability's roll behind a talent is the talent's.</summary>
    public static IReadOnlyList<ProcRoll> Baseline(EntityDocument document) =>
        [.. (Rolls(document) ?? []).Where(roll => roll.Talent is null)];

    /// <summary>
    /// True when the formula is the rate symbol times 1 plus each attribute of <see cref="ProcRoll.ScalesOff"/> in
    /// turn, the shape every roll in the build has, so <see cref="Rate"/> can evaluate it.
    /// </summary>
    public static bool Evaluable(ProcRoll roll) =>
        roll.Formula == ProcRoll.Rate + string.Concat(roll.ScalesOff.Select(attribute => Times + attribute + Close));

    /// <summary>The rate at the given attribute fractions, or null where the formula has another shape.</summary>
    public static double? Rate(ProcRoll roll, Func<string, double> attribute)
    {
        if (roll.ProcsPerMinute is not { } procs || !Evaluable(roll)) return null;

        return roll.ScalesOff.Aggregate(procs, (rate, name) => rate * (1 + attribute(name)));
    }

    public static ProcChanceRoll Roll(double rate, double sinceRoll, double sinceProc, bool beforeFirst, ProcPerMinuteRoll settings)
    {
        var drought = Math.Max(0, Math.Min(sinceProc, settings.LongestDrought));
        var factor = Math.Max(1, DroughtSlope * rate * drought / SecondsPerMinute - DroughtOffset);
        var basis = rate * Math.Max(0, sinceRoll) / SecondsPerMinute;
        var chance = basis * factor;
        double? floor = beforeFirst ? rate / Math.Max(settings.FirstProcChanceFloorDivisor, LeastDivisor) : null;

        if (floor is { } least) chance = Math.Max(chance, least);

        return new ProcChanceRoll(rate, basis, factor, floor, Math.Clamp(chance, 0, 1));
    }
}
