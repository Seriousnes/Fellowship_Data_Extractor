using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

using Fellowship.SDK.Client;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static partial class RecordReadings
{
    public static IResult? Refused(RecordReading reading, IEntityStoreService store) =>
        store.Export.Refusal(reading) is { } refusal ? Unreadable(refusal.Title, refusal.Detail) : null;

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
                item with { Magnitudes = Magnitudes(read, reading, store) },

            AugmentationRecord augmentation when document is AugmentationDocument read &&
                (reading.ItemLevel is not null || reading.RankSources is { Count: > 0 }) =>
                augmentation with { Equipped = Equipped(read, reading, store, tooltips) },

            CreatureRecord creature when document is CreatureDocument read && reading.Difficulty is { } difficulty =>
                creature with { Difficulty = Scaled(read, difficulty, store.Settings) },

            AbilityRecord ability when document is AbilityDocument read && reading.Difficulty is { } difficulty &&
                store.Menacing(read) is { } owner =>
                ability with
                {
                    Difficulty = new AbilityDifficultyRecord(difficulty, Difficulties.At(store.Settings, difficulty)!.Label)
                    {
                        Damage =
                        [
                            .. (read.Coefficients ?? [])
                                .Select(coefficient => (coefficient.Part, Band: Difficulties.Damage(
                                    coefficient, owner, store.Settings, difficulty)))
                                .Where(entry => entry.Band is not null)
                                .Select(entry => new DamageBandRecord(entry.Band!.Mean, entry.Band.Low, entry.Band.High)
                                {
                                    Part = entry.Part,
                                }),
                        ],
                    },
                },

            GlyphRecord glyph when document is GlyphDocument read && reading.EquippedTier is { } tier =>
                glyph with
                {
                    Equipped = new EquippedGlyphRecord(tier, store.Settings.Glyphs.Tiers)
                    {
                        Description = read.Description is { } text
                            ? Prose(tooltips.Text(
                                text,
                                read.Placeholders,
                                new TooltipQuery { EquippedTier = tier },
                                null,
                                FellowshipCodex.Schools(read)).Value)
                            : null,
                    },
                },

            _ => (EntityRecord)record,
        });

    private static ItemMagnitudesRecord Magnitudes(ItemDocument item, RecordReading reading, IEntityStoreService store) =>
        store.Export.Magnitudes(item, new ItemScaling
        {
            Hero = reading.Hero,
            ItemLevel = reading.ItemLevel,
            Upgrades = reading.Upgrades,
            Rarity = reading.Rarity,
        });

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

        var rank = AugmentationRanks.Rank(store.Export, query.Equipped, augmentation);
        var level = augmentation is EnchantDocument ? AugmentationRanks.Level(settings, query.Equipped) : (int?)null;

        return new EquippedAugmentationRecord
        {
            Rank = rank,
            RankSources = [.. AugmentationRanks.Sources(store.Export, query.Equipped, augmentation).Select(source => source.Type)],
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
