using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

namespace Fellowship.Codex.Endpoints;

public static class PageQuery
{
    public static IReadOnlyDictionary<string, string> First(Dictionary<string, StringValues> parsed)
    {
        var first = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, values) in parsed)
        {
            if (values.FirstOrDefault() is { Length: > 0 } value) first[key] = value;
        }

        return first;
    }

    public static TooltipQuery Read(
        Dictionary<string, StringValues> raw, IEntityStoreService store, string? hero, EntityDocument document)
    {
        var values = First(raw);

        var stats = StatQuery.FromQuery(
            new QueryCollection(raw), RequestProperties.OfEveryTooltip, store.Stats, out _);

        return StatQuery.Read(stats, store.Stats, out _) with
        {
            ShowIcon = true,
            Hero = hero ?? StatQuery.NullIfEmpty(values.GetValueOrDefault("hero")),
            Rarity = StatQuery.NullIfEmpty(values.GetValueOrDefault("rarity")),
            ItemLevel = Number(values, "itemLevel"),
            Upgrades = Number(values, "upgrades"),
            MaxUpgrades = Number(values, "maxUpgrades"),
            NoSocket = values.ContainsKey("noSocket"),
            Gem = Number(values, "gem"),
            AllocatedAttunements = Number(values, "allocatedAttunements"),
            Modifiers = ItemModifiers.TryParse(values.GetValueOrDefault("modifiers"), out var modifiers)
                ? modifiers.Values
                : [],
            Attunements = Attunements.TryParse(values.GetValueOrDefault("attunements"), out var attuned)
                ? attuned.Values
                : [],
            State = StatQuery.NullIfEmpty(values.GetValueOrDefault("state"))
                ?? (document is GlyphDocument ? ChromeAssetService.GlyphState(1) : null),
            RankSources = RankSources.Parse(values.GetValueOrDefault("rankSources")),
            Difficulty = Number(values, "difficulty"),
        };
    }

    private static int? Number(IReadOnlyDictionary<string, string> values, string key) =>
        values.GetValueOrDefault(key) is { } value && int.TryParse(value, out var number) ? number : null;
}
