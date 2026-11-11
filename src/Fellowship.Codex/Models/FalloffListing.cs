using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class FalloffListing
{
    extension(DamageFalloff)
    {
        public static IReadOnlyList<FalloffSource> Sources(IEntityStoreService store) =>
        [
            .. store.Entities
                .Select(DamageFalloff.Source)
                .OfType<FalloffSource>()
                .DistinctBy(source => (source.Type, source.Document.Id))
                .OrderBy(source => PublicTypes.NameOf(source.Document), StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.Type)
                .ThenBy(source => source.Document.Id),
        ];

        public static FalloffSource? Source(IEntityStoreService store, EntityType type, int? id) =>
            id is { } wanted
                ? store.All(type, wanted).Select(DamageFalloff.Source).OfType<FalloffSource>().FirstOrDefault()
                : null;
    }
}
