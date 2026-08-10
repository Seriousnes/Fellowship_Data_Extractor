namespace FellowshipExtractor.Tooltip;

public readonly record struct RarityImage(string Rarity, string Image);

public delegate IReadOnlyList<RarityImage> HeroArt(string heroCodename);

public static class ItemVariants
{
    public static List<Entity> Expand(
        Entity item,
        ItemNaming naming,
        HeroNames heroes,
        ItemSetBonuses setBonuses,
        string? setBonusMappingKey,
        HeroArt art)
    {
        var declared = item.Variants is { Count: > 0 } variants ? variants : null;
        var eligible = declared?.Select(variant => variant.Hero) ?? heroes.Codenames;

        var relative = item.Stats?.FirstOrDefault(slot => slot.Kind == SlotKinds.Primary);
        var cards = new List<Card>();

        foreach (var codename in eligible)
        {
            if (heroes.Profile(codename) is not { } hero) continue;

            var variant = declared?.FirstOrDefault(v => v.Hero.Equals(codename, StringComparison.OrdinalIgnoreCase));
            var (name, armorType) = Naming(naming, hero, variant, item);

            var stats = relative is null
                ? item.Stats
                : item.Stats!.Select(slot => slot == relative ? slot with { Stat = hero.PrimaryStat } : slot)
                    .ToArray();

            cards.Add(new Card
            {
                DisplayName = name,
                ArmorType = armorType,
                PrimaryStat = relative is null ? null : hero.PrimaryStat,
                Hero = hero.ShippedName,
                Codename = codename,
                Stats = stats,
                Grants = variant?.Grants ?? item.Grants,
                GrantsId = variant?.GrantsId ?? item.GrantsId,
                Art = art(codename),
            });
        }

        if (cards.Count == 0) return [item with { Variants = null }];

        return
        [
            .. cards
                .OrderBy(card => card.ArmorType, StringComparer.Ordinal)
                .ThenBy(card => card.PrimaryStat, StringComparer.Ordinal)
                .ThenBy(card => card.Hero, StringComparer.Ordinal)
                .Select(card => item with
                {
                    DisplayName = card.DisplayName,
                    ArmorType = card.ArmorType,
                    PrimaryStat = card.PrimaryStat,
                    Heroes = [card.Hero],
                    SetBonus = setBonuses.ForHeroes(setBonusMappingKey, [card.Codename]),
                    Stats = card.Stats,
                    Grants = card.Grants,
                    GrantsId = card.GrantsId,
                    Icon = Best(card.Art) ?? item.Icon,
                    IconByRarity = Spread(card.Art),
                    Variants = null,
                }),
        ];
    }

    private static string? Best(IReadOnlyList<RarityImage> art) =>
        art.Count == 0 ? null : art[^1].Image;

    private static IReadOnlyDictionary<string, string>? Spread(IReadOnlyList<RarityImage> art)
    {
        if (art.Count == 0) return null;

        var spread = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in art) spread[entry.Rarity] = entry.Image;
        return spread;
    }

    private static (string? Name, string? ArmorType) Naming(
        ItemNaming naming, HeroProfile hero, Variant? variant, Entity item)
    {
        if (naming.ByHero.TryGetValue(hero.Codename, out var perHero))
        {
            return (perHero, hero.ArmorType);
        }

        if (naming.Unambiguous is { } single)
        {
            return (single, naming.UnambiguousArmorType);
        }

        if (hero.ArmorType is { } armorType && naming.ByArmorType.TryGetValue(armorType, out var perArmorType))
        {
            return (perArmorType, armorType);
        }

        return (variant?.DisplayName ?? naming.Scalar ?? item.DisplayName, null);
    }

    private sealed record Card
    {
        public string? DisplayName { get; init; }
        public string? ArmorType { get; init; }
        public string? PrimaryStat { get; init; }
        public required string Hero { get; init; }
        public required string Codename { get; init; }
        public IReadOnlyList<StatSlot>? Stats { get; init; }
        public Entity? Grants { get; init; }
        public int? GrantsId { get; init; }
        public required IReadOnlyList<RarityImage> Art { get; init; }
    }
}
