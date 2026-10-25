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
            Hero = hero ?? StatQuery.NullIfEmpty(Value(values, ReadingParameter.Hero)),
            Rarity = StatQuery.NullIfEmpty(Value(values, ReadingParameter.Rarity)),
            ItemLevel = Number(values, ReadingParameter.ItemLevel),
            Upgrades = Number(values, ReadingParameter.Upgrades),
            MaxUpgrades = Number(values, ReadingParameter.MaxUpgrades),
            NoSocket = values.ContainsKey(QueryKeys.Key(ReadingParameter.NoSocket)),
            Gem = Number(values, ReadingParameter.Gem),
            AllocatedAttunements = Number(values, ReadingParameter.AllocatedAttunements),
            Modifiers = ItemModifiers.TryParse(Value(values, ReadingParameter.Modifiers), out var modifiers)
                ? modifiers.Values
                : [],
            Attunements = Attunements.TryParse(Value(values, ReadingParameter.Attunements), out var attuned)
                ? attuned.Values
                : [],
            State = StatQuery.NullIfEmpty(Value(values, ReadingParameter.State))
                ?? (document is GlyphDocument ? QueryKeys.Key(ChromeAssetService.GlyphStateFor(1)) : null),
            RankSources = RankSources.Parse(Value(values, ReadingParameter.RankSources)),
            Difficulty = Number(values, ReadingParameter.Difficulty),
        };
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, ReadingParameter parameter) =>
        values.GetValueOrDefault(QueryKeys.Key(parameter));

    private static int? Number(IReadOnlyDictionary<string, string> values, ReadingParameter parameter) =>
        Value(values, parameter) is { } value && int.TryParse(value, out var number) ? number : null;
}
