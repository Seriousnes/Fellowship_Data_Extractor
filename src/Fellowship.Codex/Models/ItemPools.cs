using Fellowship.Codex.Services;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace Fellowship.Codex.Models;

public static class ItemPools
{
    /// <summary>Items from one source, one reading per id. <paramref name="hero"/>'s reading leads where one exists.</summary>
    public static IReadOnlyList<ItemDocument> Of(
        ICodexService codex, IEntityStoreService store, ItemSource source, string? hero)
    {
        var wearer = hero is null ? null : store.Hero(hero);

        return
        [
            .. codex.Of(EntityType.Item)
                .OfType<ItemDocument>()
                .Where(item => item.Sources?.Contains(source) is true && codex.Flags.All(flag => flag.Admits(item)))
                .GroupBy(item => item.Id)
                .Select(readings =>
                    readings.FirstOrDefault(item => wearer is not null && EntityStore.Reads(item, wearer)) ??
                    readings.First())
                .OrderBy(item => item.Slot, StringComparer.OrdinalIgnoreCase)
                .ThenBy(PublicTypes.NameOf, StringComparer.OrdinalIgnoreCase),
        ];
    }
}
