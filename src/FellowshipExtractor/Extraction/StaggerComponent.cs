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
