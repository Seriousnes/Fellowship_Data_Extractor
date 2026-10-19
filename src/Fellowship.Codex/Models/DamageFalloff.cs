using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

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

    public static IReadOnlyList<FalloffSource> Sources(IEntityStoreService store) =>
    [
        .. store.Entities
            .Select(Source)
            .OfType<FalloffSource>()
            .DistinctBy(source => (source.Type, source.Document.Id))
            .OrderBy(source => PublicTypes.NameOf(source.Document), StringComparer.OrdinalIgnoreCase)
            .ThenBy(source => source.Type)
            .ThenBy(source => source.Document.Id),
    ];

    public static FalloffSource? Source(IEntityStoreService store, EntityType type, int? id) =>
        id is { } wanted
            ? store.All(type, wanted).Select(Source).OfType<FalloffSource>().FirstOrDefault()
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
