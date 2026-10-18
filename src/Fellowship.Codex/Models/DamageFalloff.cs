using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record DamageFalloffSplit(int Threshold, double Targets, double Scaler, double PerTarget, double Total);

public static class DamageFalloff
{
    public static IReadOnlyList<AbilityDocument> Abilities(IEntityStoreService store) =>
    [
        .. store.Entities
            .OfType<AbilityDocument>()
            .Where(ability => ability.TargetCountFalloff is { Count: > 0 })
            .DistinctBy(ability => ability.Id)
            .OrderBy(ability => PublicTypes.NameOf(ability), StringComparer.OrdinalIgnoreCase)
            .ThenBy(ability => ability.Id),
    ];

    public static AbilityDocument? Ability(IEntityStoreService store, int? id) =>
        id is { } wanted
            ? store.All(EntityType.Ability, wanted)
                .OfType<AbilityDocument>()
                .FirstOrDefault(ability => ability.TargetCountFalloff is { Count: > 0 })
            : null;
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
