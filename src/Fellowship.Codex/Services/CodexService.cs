using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Models;

using static Fellowship.Codex.Models.FellowshipCodex;

namespace Fellowship.Codex.Services;

public interface ICodexService
{
    IReadOnlyList<Facet> Facets { get; }

    IReadOnlyList<Flag> Flags { get; }

    IReadOnlyList<Measure> Measures { get; }

    IReadOnlyList<Sorting> Sortings { get; }

    IReadOnlyList<TypeView> Types { get; }

    IReadOnlyList<TypeView> Tabs(Category category);

    int Count { get; }

    IReadOnlyList<EntityDocument> Of(EntityType type);

    FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows);

    FacetCount Tally(Facet facet, IReadOnlyList<EntityDocument> rows);

    string Printed(Facet facet, string value);

    IReadOnlyList<string> Offered(Facet facet);

    Facet Facet(FacetType type);

    Measure Measure(MeasureType type);

    Flag Flag(FlagType type);

    Sorting Sorting(SortingType type);

    IEnumerable<string> Scaling(IReadOnlyList<string>? attributes);
}
public sealed class CodexService : ICodexService
{
    private readonly IEntityStoreService store;
    private readonly IDesignTokenService tokens;

    private readonly IReadOnlyDictionary<FacetType, string[]> offered;

    private readonly IReadOnlyDictionary<FacetType, Facet> facets;

    private readonly IReadOnlyDictionary<FlagType, Flag> flags;

    private readonly IReadOnlyDictionary<MeasureType, Measure> measures;

    private readonly IReadOnlyDictionary<SortingType, Sorting> sortings;

    public IReadOnlyList<Facet> Facets { get; }

    public IReadOnlyList<Flag> Flags { get; }

    public IReadOnlyList<Measure> Measures { get; }

    public IReadOnlyList<Sorting> Sortings { get; }

    public IReadOnlyList<TypeView> Types { get; }

    public int Count { get; }

    public IReadOnlyList<TypeView> Tabs(Category category) =>
        [.. Types.Where(type => type.Category == category).OrderBy(type => type.Position)];

    public CodexService(IEntityStoreService store, IDesignTokenService tokens)
    {
        this.store = store;
        this.tokens = tokens;

        VerifySlots(store);

        Facets =
        [
            new Facet(FacetType.Slot, document => document switch
            {
                ItemDocument item => One(item.Slot),
                GlyphDocument { Slots: { } slots } => slots,
                _ => [],
            })
            {
                Primary = true,
            },

            new Facet(FacetType.Kind, document =>
                One(document is ItemDocument item ? Kind(item) : null)),

            new Facet(FacetType.Armor, document =>
                One(document is ItemDocument { ArmorType: { } armor } ? armor.ToString() : null)),

            new Facet(FacetType.Stat, document =>
                One(document is ItemDocument { PrimaryStat: { } stat } ? stat.ToString() : null)),

            new Facet(FacetType.Hero, document => document switch
            {
                ItemDocument { Heroes: { } heroes } => heroes.Select(hero => hero.ToString()),
                AbilityDocument { Heroes: { } heroes } => heroes.Select(hero => hero.ToString()),
                EffectDocument { Heroes: { } heroes } => heroes.Select(hero => hero.ToString()),
                TalentSlotDocument slot => [slot.Hero.ToString()],
                TalentDocument talent when store.Slot(talent.Id) is { } slot => [slot.Hero.ToString()],
                _ => [],
            })
            {
                Colour = (t, value) => t.Hero(value),
                Primary = true,
            },

            new Facet(FacetType.Source, document => document switch
            {
                ItemDocument { Sources: { } sources } => sources.Select(source => source.Name),
                AbilityDocument { Owner: { } owner } => One(Owner(store, owner)),
                EffectDocument { Owner: { } owner } => One(Owner(store, owner)),
                CreatureDocument { FoundIn: { } dungeons } => dungeons.Select(level => Owner(store, level)).OfType<string>(),
                _ => [],
            }),

            new Facet(FacetType.Rank, document =>
                One(document is CreatureDocument { Rank: { } rank } ? rank.ToString() : null)),

            new Facet(FacetType.School, Schools),
            new Facet(FacetType.Resource, document =>
                One(document is AbilityDocument { Resource: { } resource } ? resource : null)),

            new Facet(FacetType.Gem, document => One(document switch
            {
                RadianceDocument gem => gem.Type.ToString(),
                GemDocument gem => gem.Type.ToString(),
                GemAbilityDocument gem => gem.Gem.ToString(),
                _ => null,
            }))
            {
                Colour = (t, value) => t.Gem(value),
                Primary = true,
            },

            new Facet(FacetType.Rarity, document => document switch
            {
                ItemDocument { Rarities: { Count: > 0 } rarities } =>
                    rarities.Select(tier => RarityWord(store, tier, gem: false)),
                ItemDocument { Rarity: { } rarity } => One(RarityWord(store, rarity, gem: false)),
                CurrencyDocument { Rarity: { } coin } => One(RarityWord(store, coin, gem: false)),
                GemDocument stone => One(RarityWord(store, stone.Rarity, gem: true)),
                _ => [],
            })
            {
                Colour = (t, value) => t.Rarity(Stored(store, value)),
                Primary = true,
            },
        ];

        Flags =
        [
            new Flag(FlagType.Unobtainable, document =>
                PublicTypes.DisplayNameOf(document) is null || document.EnabledInGame is false)
            {
                Hides = true,
            },
            new Flag(FlagType.Icon, document => DocumentFields.Text(document, nameof(ItemDocument.Icon)) is not null),
            new Flag(FlagType.Described, document =>
                DocumentFields.Text(document, nameof(ItemDocument.Description)) is not null),
            new Flag(FlagType.Legendary, document =>
                document is ItemDocument { Rarities: { } rarities } && rarities.Contains(Rarity.Legendary)),
            new Flag(FlagType.Grants, document => document is ItemDocument { Grants: not null }),
            new Flag(FlagType.Socket, document => document is ItemDocument { GemSlot: true }),
            new Flag(FlagType.Unique, document => document is ItemDocument { UniqueEquipped: true }),
        ];

        Measures =
        [
            new Measure(MeasureType.Cooldown, document => DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)))
            {
                Unit = "s",
            },
            new Measure(MeasureType.Cast, document => DocumentFields.Number(document, nameof(AbilityDocument.CastTime)))
            {
                Unit = "s",
            },
            new Measure(MeasureType.Channel, document => DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)))
            {
                Unit = "s",
            },
            new Measure(MeasureType.Range, document => DocumentFields.Number(document, nameof(AbilityDocument.Range))),
            new Measure(MeasureType.Radius, document => DocumentFields.Number(document, nameof(AbilityDocument.Radius))),
            new Measure(MeasureType.Duration, document => DocumentFields.Number(document, nameof(AbilityDocument.Duration)))
            {
                Unit = "s",
            },
            new Measure(MeasureType.Cost, document => DocumentFields.Number(document, nameof(AbilityDocument.Cost))),
            new Measure(MeasureType.Charges, document => DocumentFields.Number(document, nameof(AbilityDocument.Charges))),
            new Measure(MeasureType.Rank, document => DocumentFields.Number(document, nameof(AugmentationDocument.MaxRank))),

            new Measure(MeasureType.Id, document => (double?)document.Id),
        ];

        Sortings =
        [
            new Sorting(SortingType.Relevance, PublicTypes.NameOf),

            new Sorting(SortingType.Name, PublicTypes.NameOf),
            new Sorting(SortingType.Type, document => PublicTypes.Label(EntityTypes.TypeOf(document))),

            new Sorting(SortingType.Slot, document => (document as ItemDocument)?.Slot),
            new Sorting(SortingType.Ilvl, document => (document as ItemDocument)?.ItemLevel),
            new Sorting(SortingType.Kind, document => document is ItemDocument item ? Kind(item) : null),

            new Sorting(SortingType.Stat, document => document switch
            {
                ItemDocument { PrimaryStat: { } stat } => stat.ToString(),
                EnchantDocument { PrimaryStat: { } stat } => stat.ToString(),
                _ => null,
            }),

            new Sorting(SortingType.Source, document => document switch
            {
                ItemDocument { Sources: [var first, ..] } => first.Name,
                AbilityDocument { Owner: { } owner } => Owner(store, owner),
                EffectDocument { Owner: { } owner } => Owner(store, owner),
                _ => null,
            }),

            new Sorting(SortingType.Location, document =>
                document is CreatureDocument { FoundIn: [var dungeon, ..] } ? Owner(store, dungeon) : null),

            new Sorting(SortingType.Rank, document => document switch
            {
                CreatureDocument { Rank: { } rank } => (int)rank,
                _ => DocumentFields.Number(document, nameof(AugmentationDocument.MaxRank)),
            }),

            new Sorting(SortingType.Health, document => (document as CreatureDocument)?.Health),

            new Sorting(SortingType.Qualifier, document => (document as DungeonDocument)?.QualifierTime),

            new Sorting(SortingType.Score, document => (document as DungeonDocument)?.RequiredScore),

            new Sorting(SortingType.Hero, document => document switch
            {
                ItemDocument { Heroes: [var wearer, ..] } => wearer.ToString(),
                AbilityDocument { Heroes: [var caster, ..] } => caster.ToString(),
                EffectDocument { Heroes: [var carrier, ..] } => carrier.ToString(),
                TalentSlotDocument slot => slot.Hero.ToString(),
                TalentDocument talent => store.Slot(talent.Id)?.Hero.ToString(),
                _ => null,
            }),

            new Sorting(SortingType.School, document => Schools(document).FirstOrDefault()),

            new Sorting(SortingType.Applier, document =>
                document is EffectDocument { AppliedBy: { } applier } ? Owner(store, applier) : null),

            new Sorting(SortingType.Gem, document => document switch
            {
                RadianceDocument gem => gem.Type.ToString(),
                GemDocument gem => gem.Type.ToString(),
                GemAbilityDocument gem => gem.Gem.ToString(),
                _ => null,
            }),

            new Sorting(SortingType.Power, document => (document as GemAbilityDocument)?.RequiredPower),

            new Sorting(SortingType.Tier, document => document switch
            {
                TalentSlotDocument slot => slot.Row,
                TalentDocument talent => store.Slot(talent.Id)?.Row,
                _ => null,
            }),

            new Sorting(SortingType.Cost, document => document switch
            {
                TalentDocument talent when store.Slot(talent.Id) is { } slot => (double)slot.Cost,
                TalentSlotDocument slot => (double)slot.Cost,
                _ => DocumentFields.Number(document, nameof(AbilityDocument.Cost)),
            }),

            new Sorting(SortingType.Rarity, document => document switch
            {
                ItemDocument { Rarity: { } rarity } => (int)rarity,
                CurrencyDocument { Rarity: { } coin } => (int)coin,
                GemDocument stone => (int)stone.Rarity,
                _ => null,
            }),

            new Sorting(SortingType.Category, document => (document as AugmentationDocument)?.Category),
        ];

        facets = Defined(Facets, facet => facet.Type);
        flags = Defined(Flags, flag => flag.Type);
        measures = Defined(Measures, measure => measure.Type);
        sortings = Defined(Sortings, sorting => sorting.Type);

        var counts = store.Entities
            .Where(document => PublicTypes.IsListed(document))
            .GroupBy(EntityTypes.TypeOf)
            .ToDictionary(group => group.Key, group => group.Count());

        Count = counts.Values.Sum();

        Types =
        [
            .. PublicTypes.Listed
                .Select(type => new TypeView(
                    type.Type, type.Slug, type.Label, type.Category,
                    counts.GetValueOrDefault(type.Type), type.Position))
                .Where(type => type.Count > 0),
        ];

        var listed = store.Entities.Where(document => PublicTypes.IsListed(document)).ToArray();

        offered = Facets.ToDictionary(
            facet => facet.Type,
            facet => Tally(facet, listed).Values.Select(value => value.Value).ToArray());
    }

    public IReadOnlyList<EntityDocument> Of(EntityType type) =>
        [.. store.Entities.Where(document => EntityTypes.TypeOf(document) == type)];

    private static IReadOnlyDictionary<TType, TDefined> Defined<TType, TDefined>(
        IReadOnlyList<TDefined> defined, Func<TDefined, TType> type) where TType : struct, Enum
    {
        var missing = Enum.GetValues<TType>().Except(defined.Select(type)).ToArray();
        var twice = defined.GroupBy(type).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();

        if (missing.Length > 0 || twice.Length > 0)
        {
            throw new InvalidOperationException(
                $"Each {typeof(TType).Name} member has one {typeof(TDefined).Name}, and " +
                $"{string.Join(", ", missing.Concat(twice))} {(missing.Length + twice.Length == 1 ? "does" : "do")} not, " +
                "so the rail would offer a key that answers nothing or answers twice.");
        }

        return defined.ToDictionary(type);
    }

    public FacetView View(Facet facet, IReadOnlyList<EntityDocument> rows) =>
        new(facet.Key, facet.Label, Tally(facet, rows).Values);

    public FacetCount Tally(Facet facet, IReadOnlyList<EntityDocument> rows)
    {
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var value in facet.Values(row))
            {
                if (value.Length == 0) continue;

                counted[value] = counted.GetValueOrDefault(value) + 1;
            }
        }

        return new FacetCount(facet,
        [
            .. counted
                .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(spellings => (
                    Value: Spelling(spellings),
                    Count: spellings.Sum(pair => pair.Value)))
                .OrderByDescending(value => value.Count)
                .ThenBy(value => value.Value, StringComparer.OrdinalIgnoreCase)
                .Select(value => new FacetValue(
                    value.Value,
                    Printed(facet, value.Value),
                    value.Count,
                    CodexView.Readable(facet.Colour?.Invoke(tokens, value.Value)))),
        ]);
    }

    private static string Spelling(IEnumerable<KeyValuePair<string, int>> spellings) =>
        spellings
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First()
            .Key;

    public string Printed(Facet facet, string value) => Spaced(value);

    public IReadOnlyList<string> Offered(Facet facet) => offered[facet.Type];

    public Facet Facet(FacetType type) => facets[type];

    public Measure Measure(MeasureType type) => measures[type];

    public Flag Flag(FlagType type) => flags[type];

    public Sorting Sorting(SortingType type) => sortings[type];

    private static IEnumerable<string> One(string? value) => value is null ? [] : [value];

    public const string WeaponSlot = "Weapon";

    public static readonly string[] SlotWords = ["Neck", "Ring", "Relic"];

    public static string? Kind(ItemDocument item) => item.Slot switch
    {
        null => null,
        { } slot when string.Equals(slot, WeaponSlot, StringComparison.OrdinalIgnoreCase) =>
            (item.Hero ?? item.Heroes?.FirstOrDefault())?.ToString(),
        { } slot when SlotWords.Contains(slot, StringComparer.OrdinalIgnoreCase) => slot,
        _ => item.ArmorType?.ToString(),
    };

    private static void VerifySlots(IEntityStoreService store)
    {
        var slots = store.Settings.StatCalculation.SlotWeights.Keys;

        var missing = SlotWords.Append(WeaponSlot)
            .Where(named => !slots.Contains(named, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "is" : "are")} not a slot the build weights, " +
                $"so {nameof(CodexService)}.{nameof(Kind)} would read an armour type onto a slot that has none. " +
                $"The slots are {string.Join(", ", slots)}.");
        }
    }

    public IEnumerable<string> Scaling(IReadOnlyList<string>? attributes) =>
        (attributes ?? []).Select(store.StatName).Distinct(StringComparer.Ordinal);

    private static string? Owner(IEntityStoreService store, EntityReference owner) =>
        owner.Name
        ?? (store.All(owner.Type, owner.Id).FirstOrDefault() is { } document
            ? PublicTypes.NameOf(document)
            : null);

    private static string RarityWord(IEntityStoreService store, Rarity rarity, bool gem) =>
        store.Settings.Rarities.FirstOrDefault(r => r.Tier == (int)rarity) is { } entry
            ? (gem ? entry.GemDisplayName : entry.DisplayName)
            : rarity.ToString();

    private static string Stored(IEntityStoreService store, string printed) =>
        store.Rarity(printed, gem: true)?.Name ?? printed;

}
