using CUE4Parse.FileProvider;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The <c>StaggerActorComponent</c> default of <c>BP_PlayerCharacter</c>: the rows its period, bucket count and cap read,
/// and the effect a tick applies. The hero Blueprints override none of them.
/// </summary>
public static class StaggerComponent
{
    public const string Package = "/Game/characters/Player/BP_PlayerCharacter";
    private const string ComponentName = "StaggerActorComponent";
    private const string ComponentClass = "CRStaggerActorComponent";

    // Binary 26320967: tick 0x1452CDD70, bucket spread 0x1452CF830, pool setter 0x1452C3540.
    private const string Rule =
        "A part p added part-way through a period puts p·f/(n−1+f) in the first bucket and p/(n−1+f) in each other " +
        "bucket, f the share of the period left. While the pool holds Stagger, each period deals the first bucket " +
        "(at most the pool) and shifts the buckets forward. The period's countdown pauses while the pool is empty and " +
        "resumes at the next add. A removal outside a tick scales every bucket by the share of Stagger left.";

    // Binary 26320967: the cap in the pool setter 0x1452C35CD to 0x1452C3618, MaxHealth through the character's
    // virtual +0xA38 (0x1452EC9A0), the hit's own reduction in the damage hook 0x1452C87E9 to 0x1452C87FA.
    private const string Cap =
        "An add takes min(part, cap − pool), with cap = maxHealthPercentage × the owner's MaxHealth at that add. " +
        "A pool above the cap takes no part of an add and stays as it is. A hit keeps the part the pool does not take " +
        "as Health damage. A removal has no cap.";

    // Binary 26320967: heal hook 0x1452C8900, CRModifyStaggerExecution 0x1451091E0, death clear 0x145312378. Both
    // factor attributes are 0 in the CRHeroAttributeSet constructor 0x14500A430.
    private const string Removal =
        "A delta d ≤ 0 sets the pool to max(pool + d, 0) and scales every bucket by the share of Stagger left. " +
        "A heal with healAffectStaggerTag removes clamp(heal × healReductionFactorAttribute, 0, pool), and the heal " +
        "loses that amount. A factor of 0 removes none. modifyExecution adds the magnitude of modifySetByCallerTag " +
        "unscaled. A tick removes the first bucket. The owner's death empties the pool.";

    // Binary 26320967: AuthUpdateStaggerAndLog 0x1452C3670 calls the combat logger at 0x145298560, the event variant
    // of the attribute resource writer 0x145297E00.
    private const string LoggedChange =
        "Each modifyExecution delta writes a RESOURCE_CHANGED line of resource type loggedResourceType. Its amount is " +
        "the pool after the delta, and its change is the pool's change. A hit's part, a heal's removal, a tick and " +
        "the death write no such line.";

    private const string MaxHealth = "MaxHealth";
    private const string DamageAdditionFactor = "StaggerDamageAdditionFactor";
    private const string HealReductionFactor = "StaggerHealReductionFactor";
    private const string HealAffectStagger = "EffectLogic.HealAffectStagger";
    private const string Environmental = "EffectType.Environmental";
    private const string DamageIgnoreStagger = "EffectLogic.DamageIgnoreStagger";
    private const string ModifyTag = "SetByCaller.Stagger";
    private const int LoggedResource = 7;

    public static StaggerSettings Read(AbstractFileProvider provider, ConstantsTable constants)
    {
        if (!provider.TryLoadPackage(Package, out var package))
        {
            throw new InvalidOperationException($"No container has {Package}.");
        }

        var component = package.GetExports().FirstOrDefault(export =>
                export.Name.Equals(ComponentName, StringComparison.Ordinal) &&
                export.ExportType.Equals(ComponentClass, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{Package} has no {ComponentName} of {ComponentClass}.");

        var (period, periodRow) = Scalable(component, "StaggerPeriodRef", constants);
        var (duration, durationRow) = Scalable(component, "StaggerDurationRef", constants);
        var (cap, capRow) = Scalable(component, "StaggerMaxHealthPercentage", constants);

        return new StaggerSettings
        {
            Period = period,
            PeriodRow = periodRow,
            Buckets = (int)Math.Ceiling(duration),
            BucketsRow = durationRow,
            MaxHealthPercentage = cap,
            MaxHealthPercentageRow = capRow,
            DamageEffect = Leaf(PropertyWalk.GetString(component, "DamageEffectClass"))
                ?? throw new InvalidOperationException($"{Package}'s {ComponentName} names no DamageEffectClass."),
            DamageSetByCallerTag = PropertyWalk.GetString(component, "DamageEffectSetByCallerTag")
                ?? throw new InvalidOperationException($"{Package}'s {ComponentName} names no DamageEffectSetByCallerTag."),
            DisplayText = Rule,
            MaxHealthAttribute = MaxHealth,
            DamageAdditionFactorAttribute = DamageAdditionFactor,
            HealReductionFactorAttribute = HealReductionFactor,
            HealAffectStaggerTag = HealAffectStagger,
            DamageIgnoreStaggerTags = [Environmental, DamageIgnoreStagger],
            ModifyExecution = StaggerDeltas.Execution,
            ModifySetByCallerTag = ModifyTag,
            LoggedResourceType = LoggedResource,
            CapText = Cap,
            RemovalText = Removal,
            LoggedChangeText = LoggedChange,
        };
    }

    private static (double Value, string Row) Scalable(
        CUE4Parse.UE4.Assets.Exports.UObject component, string field, ConstantsTable constants)
    {
        var scalable = PropertyWalk.GetStruct(component, field)
            ?? throw new InvalidOperationException($"{Package}'s {ComponentName} sets no {field}.");

        var (value, row) = MagnitudeCalculations.Read(scalable, constants);

        return (value ?? throw new InvalidOperationException($"{Package}'s {field} reads no constants row value."),
            row ?? throw new InvalidOperationException($"{Package}'s {field} reads no row."));
    }

    private static string? Leaf(string? path) =>
        path?[(path.LastIndexOf('.') + 1)..] is { } leaf && leaf.EndsWith("_C", StringComparison.Ordinal) ? leaf[..^2] : null;
}
