using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.SDK.Calculations;

public sealed record DamageFalloffSplit(int Threshold, double Targets, double Scaler, double PerTarget, double Total);

public sealed record FalloffSource(EntityDocument Document, IReadOnlyList<TargetCountFalloff> Entries, double? MaxTargets)
{
    public EntityType Type => EntityTypes.TypeOf(Document);

    public string Key => EntityTypes.Slug(Type);
}

public static class DamageFalloff
{
    public static FalloffSource? Source(EntityDocument document) => document switch
    {
        AbilityDocument { TargetCountFalloff: { Count: > 0 } entries } ability =>
            new FalloffSource(ability, entries, ability.MaxTargets),
        TalentDocument { TargetCountFalloff: { Count: > 0 } entries } talent =>
            new FalloffSource(talent, entries, null),
        _ => null,
    };

    public static int Whole(double value) => Math.Max(0, (int)Math.Truncate(value));

    public static double Hit(int targets, double? maxTargets) =>
        maxTargets is { } cap ? Math.Max(0, Math.Min(targets, cap)) : Math.Max(0, targets);

    public static double Scaler(int threshold, double targets) =>
        targets > threshold ? Math.Sqrt(threshold / targets) : 1;

    public static DamageFalloffSplit Split(double threshold, double? maxTargets, double damage, int targets)
    {
        var ceiling = Whole(threshold);
        var hit = Hit(targets, maxTargets);
        var scaler = Scaler(ceiling, hit);
        var each = damage * scaler;

        return new DamageFalloffSplit(ceiling, hit, scaler, each, each * hit);
    }
}
