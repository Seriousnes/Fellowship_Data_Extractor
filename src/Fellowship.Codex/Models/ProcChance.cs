using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

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

    public bool Is(EntityType type, int id) =>
        (Type == type && Face.Id == id) || (EntityTypes.TypeOf(Carrier) == type && Carrier.Id == id);
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
        _ => null,
    };

    /// <summary>The rolls the document makes in its own right: an ability's roll behind a talent is the talent's.</summary>
    public static IReadOnlyList<ProcRoll> Baseline(EntityDocument document) =>
        [.. (Rolls(document) ?? []).Where(roll => roll.Talent is null)];

    /// <summary>
    /// Every proc, one per thing a player holds or casts. A relic passive is offered as each relic granting it, one per
    /// relic name, and an ability without an icon of its own as the one augmentation it comes from.
    /// </summary>
    public static IReadOnlyList<ProcRollSource> Sources(IEntityStoreService store)
    {
        var relics = store.Entities
            .OfType<ItemDocument>()
            .Where(item => item is { Name: not null, Grants.Type: EntityType.RelicPassive })
            .GroupBy(item => item.Grants!.Id)
            .ToDictionary(group => group.Key, group => group.DistinctBy(item => item.Name).ToArray());

        var sources = new List<ProcRollSource>();

        foreach (var carrier in store.Entities)
        {
            if (carrier.Name is null || Baseline(carrier) is not { Count: > 0 } rolls) continue;

            foreach (var face in Faces(store, carrier, relics))
            {
                var source = new ProcRollSource(face, carrier, rolls);
                if (!sources.Any(other => other.Type == source.Type && other.Face.Id == source.Face.Id)) sources.Add(source);
            }
        }

        return
        [
            .. sources
                .OrderBy(source => PublicTypes.NameOf(source.Face), StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.Type)
                .ThenBy(source => source.Face.Id),
        ];
    }

    private static IEnumerable<EntityDocument> Faces(
        IEntityStoreService store, EntityDocument carrier, IReadOnlyDictionary<int, ItemDocument[]> relics)
    {
        switch (carrier)
        {
            case RelicPassiveDocument relic when relics.TryGetValue(relic.Id, out var items):
                return items;

            case AbilityDocument { Icon: null, Origins: [{ } origin] }
                when origin.Type != EntityType.Hero && store.All(origin.Type, origin.Id) is [var from, ..]:
                return [from];

            default:
                return [carrier];
        }
    }

    public static ProcRollSource? Source(IEntityStoreService store, EntityType? type, int? id) =>
        type is { } wanted && id is { } number ? Sources(store).FirstOrDefault(source => source.Is(wanted, number)) : null;

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
