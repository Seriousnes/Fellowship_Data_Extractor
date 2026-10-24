using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static partial class RecordReadings
{
    public static IResult? Refused(RecordReading reading, IEntityStoreService store)
    {
        var settings = store.Settings;

        if (reading.Hero is { } hero && store.Hero(hero) is null)
        {
            return Lookups.NoSuchHero(hero, settings.Heroes);
        }

        if (reading.Rarity is { } rarity && store.Rarity(rarity, gem: false) is null)
        {
            return Unreadable(
                "No such rarity.",
                $"'{rarity}' is not a rarity. The rarities are " +
                    string.Join(", ", settings.Rarities.Select(entry => $"{entry.DisplayName} ({entry.Tier})")) + ".");
        }

        if (reading.ItemLevel is { } level && (level < 1 || level > settings.Items.MaxItemLevel))
        {
            return Unreadable(
                "No such item level.",
                $"The item level is {Number(level)}, and an item level runs from 1 to " +
                    Number(settings.Items.MaxItemLevel) + ".");
        }

        if (reading.Upgrades is < 0)
        {
            return Unreadable("No such tempering.", $"The tempering is {Number(reading.Upgrades.Value)}, below 0.");
        }

        if (reading.RankSources is { Count: > 0 } sources)
        {
            var types = AugmentationRanks.Text(settings).ItemTypes.Select(type => type.Type).ToArray();

            if (sources.FirstOrDefault(source => !types.Contains(source, StringComparer.OrdinalIgnoreCase)) is { } unknown)
            {
                return Unreadable(
                    "No such rank source.",
                    $"'{unknown}' is not an item type. The item types are {string.Join(", ", types)}.");
            }
        }

        if (reading.Difficulty is { } difficulty && Difficulties.At(settings, difficulty) is null)
        {
            return Unreadable(
                "No such difficulty.",
                $"The difficulty is {Number(difficulty)}. The difficulties are " +
                    string.Join(", ", Difficulties.Ladder(settings).Select(step => Number(step.Difficulty))) + ".");
        }

        return null;
    }

    public static bool Keeps(EntityDocument document, RecordReading reading, IEntityStoreService store)
    {
        if (store.Hero(reading.Hero) is { } hero && !EntityStore.Reads(document, hero)) return false;

        if (document is ItemDocument item && store.Rarity(reading.Rarity, gem: false) is { } rarity)
        {
            return item.Rarities is { Count: > 0 } rarities
                ? rarities.Any(tier => (int)tier == rarity.Tier)
                : item.Rarity is { } only && (int)only == rarity.Tier;
        }

        return true;
    }

    public static TRecord Read<TRecord>(
        TRecord record,
        EntityDocument document,
        RecordReading reading,
        IEntityStoreService store,
        ITooltipViewService tooltips)
        where TRecord : EntityRecord => (TRecord)(EntityRecord)(record switch
        {
            ItemRecord item when document is ItemDocument read &&
                (reading.ItemLevel is not null || reading.Upgrades is not null || reading.Rarity is not null) =>
                item with { Magnitudes = Magnitudes(read, reading, store, tooltips) },

            AugmentationRecord augmentation when document is AugmentationDocument read &&
                (reading.ItemLevel is not null || reading.RankSources is { Count: > 0 }) =>
                augmentation with { Equipped = Equipped(read, reading, store, tooltips) },

            CreatureRecord creature when document is CreatureDocument read && reading.Difficulty is { } difficulty =>
                creature with { Difficulty = Scaled(read, difficulty, store.Settings) },

            _ => (EntityRecord)record,
        });

    private static ItemMagnitudesRecord Magnitudes(
        ItemDocument item, RecordReading reading, IEntityStoreService store, ITooltipViewService tooltips)
    {
        var query = new TooltipQuery
        {
            Hero = reading.Hero,
            ItemLevel = reading.ItemLevel,
            Upgrades = reading.Upgrades,
            Rarity = reading.Rarity,
        };

        var rarity = tooltips.Rarity(item, query);
        var level = query.ItemLevel ?? store.DropItemLevel(item);
        var at = query with { ItemLevel = level };

        return new ItemMagnitudesRecord
        {
            ItemLevel = level,
            Upgrades = query.Upgrades ?? 0,
            MaxUpgrades = Tempering.Ceiling(store.Settings.MaxTempers, rarity?.Name, level),
            Rarity = rarity is null ? null : tooltips.RarityName(item, rarity),
            PotentialPower = ItemPower.Of(item, store.Settings, rarity, at, level),
            Stats =
            [
                .. tooltips.Rolls(item, at, rarity).Select(line => new StatMagnitudeRecord(
                    line.Stat is null ? null : line.DisplayName, line.Value, line.Kind)),
            ],
        };
    }

    private static EquippedAugmentationRecord Equipped(
        AugmentationDocument augmentation,
        RecordReading reading,
        IEntityStoreService store,
        ITooltipViewService tooltips)
    {
        var settings = store.Settings;

        var query = new TooltipQuery
        {
            Hero = reading.Hero,
            ItemLevel = reading.ItemLevel,
            RankSources = reading.RankSources ?? [],
        };

        var rank = AugmentationRanks.Rank(store, query, augmentation);
        var level = augmentation is EnchantDocument ? AugmentationRanks.Level(settings, query) : (int?)null;

        return new EquippedAugmentationRecord
        {
            Rank = rank,
            RankSources = [.. AugmentationRanks.Sources(store, query, augmentation).Select(source => source.Type)],
            ItemLevel = level,
            Description = AugmentationRanks.Description(
                    augmentation, rank, AugmentationRanks.HeroRead(settings, augmentation, reading.Hero)) is { } text
                ? Prose(tooltips.Text(
                    text,
                    CodexView.PlaceholdersOf(augmentation),
                    query with { EquippedTier = rank },
                    store.Hero(reading.Hero),
                    FellowshipCodex.Schools(augmentation)).Value)
                : null,
            Stats = augmentation is EnchantDocument enchant
                ? [.. AugmentationRanks.AtRank(enchant, settings, rank)
                    .Select(granted => new GrantedStatRecord(store.StatName(granted.Stat), granted.Value))]
                : null,
        };
    }

    private static CreatureDifficultyRecord Scaled(CreatureDocument creature, int difficulty, SettingsFile settings) =>
        new(difficulty, Difficulties.At(settings, difficulty)!.Label)
        {
            Health = Difficulties.Health(creature, settings, difficulty),
        };

    private static string Prose(string html) =>
        WebUtility.HtmlDecode(Tags().Replace(Breaks().Replace(html, "\n"), string.Empty)).Trim();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex Breaks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    private static IResult Unreadable(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
