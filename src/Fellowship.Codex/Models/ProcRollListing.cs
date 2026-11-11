using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class ProcRollListing
{
    extension(ProcChance)
    {
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
                if (carrier.Name is null || ProcChance.Baseline(carrier) is not { Count: > 0 } rolls) continue;

                foreach (var face in Faces(store, carrier, relics))
                {
                    var source = new ProcRollSource(face, carrier, rolls);
                    var at = sources.FindIndex(other => other.Type == source.Type && other.Face.Id == source.Face.Id);

                    if (at < 0) sources.Add(source);
                    else if (ReferenceEquals(face, carrier)) sources[at] = source with { Others = [.. sources[at].Others, sources[at].Carrier] };
                    else sources[at] = sources[at] with { Others = [.. sources[at].Others, carrier] };
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

        public static ProcRollSource? Source(IEntityStoreService store, EntityType? type, int? id) =>
            type is { } wanted && id is { } number ? ProcChance.Sources(store).FirstOrDefault(source => source.Is(wanted, number)) : null;
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
}
