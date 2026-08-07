using System.Globalization;

using Azure.Storage.Blobs;

using Fellowship.SDK.Documents;

using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api.Models;

public sealed class TooltipView(
    EntityStore store,
    DesignTokens tokens,
    RichTextRenderer renderer,
    Placeholders placeholders,
    ChromeAssets chrome,
    BlobContainerClient icons,
    IWebHostEnvironment environment)
{
    public const string Root = "fx-tip";

    public const EntityType TraitType = EntityType.Trait;

    private const string Sheet = "css/tooltip.css";

    private readonly string sheet = Compose(environment, chrome, renderer);

    public ChromeAssets Chrome => chrome;

    public RarityEntry? Rarity(EntityDocument document, TooltipQuery query)
    {
        var tier = document switch
        {
            ItemDocument { Rarity: { } rung } => (int?)rung,
            ItemDocument { Rarities: { Count: > 0 } rungs } => (int)rungs.Max(),
            CurrencyDocument { Rarity: { } coin } => (int)coin,
            GemTierDocument gemTier => (int)gemTier.Rarity,
            _ => null,
        };

        return store.Rarity(query.Rarity, document is GemTierDocument)
            ?? (tier is { } t ? store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == t) : null);
    }

    public string Variables(RarityEntry? rarity)
    {
        var colour = rarity is null ? null : tokens.Rarity(rarity.Name);

        return
            tokens.CssVariables() +
            "--fx-frame:" + (chrome.Frame(rarity?.Name) is { } frame ? $"url({frame})" : "none") +
            ";--fx-slot:" + (chrome.IconFrame(rarity?.Name) is { } slot ? $"url({slot})" : "none") +
            ";--fx-header:" + (rarity is null ? "none" : $"url({chrome.Header(rarity.Name)})") +
            ";--fx-window:url(" + (rarity is null ? chrome.RoundedWindow : chrome.Window) +
            ");--fx-title:" + (colour ?? "var(--core-text-primary)") +
            ";";
    }

    public string? Art(EntityDocument document, RarityEntry? rarity)
    {
        var icon = rarity is not null &&
                   document is ItemDocument { IconByRarity: { } byRarity } &&
                   byRarity.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : DocumentFields.Text(document, nameof(ItemDocument.Icon));

        return icon is null ? null : $"{icons.Uri}/{icon}";
    }

    public static bool DrawsItsOwnArt(EntityDocument document) =>
        document is BlessingDocument or TraitDocument;

    public string? StateFrame(EntityDocument document, TooltipQuery query) => document switch
    {
        TraitDocument => chrome.TraitNode(query.State),
        BlessingDocument => chrome.BlessingPlate(query.State),
        _ => null,
    };

    public static string? StateFamily(EntityDocument document) => document switch
    {
        TraitDocument => "node",
        BlessingDocument => "plate",
        _ => null,
    };

    public string? StateArt(EntityDocument document, TooltipQuery query) =>
        document is BlessingDocument blessing ? chrome.BlessingArt(blessing.Icon, query.State) : null;

    public bool Locked(EntityDocument document, TooltipQuery query) =>
        document is TraitDocument &&
        string.Equals(query.State, Unavailable, StringComparison.OrdinalIgnoreCase);

    private const string Unavailable = "unavailable";

    public string LockGlyph => chrome.LockGlyph;

    public string? Ornament(RarityEntry? rarity) => rarity is null ? null : chrome.Ornament(rarity.Name);

    public string Rung(EntityDocument document, RarityEntry rarity) =>
        document is GemTierDocument ? rarity.GemDisplayName : rarity.DisplayName;

    public int? Level(EntityDocument document, TooltipQuery query) =>
        document is ItemDocument { Stats: { Count: > 0 } }
            ? query.ItemLevel ?? store.DropItemLevel(document)
            : query.ItemLevel;

    public string? Temper(TooltipQuery query, RarityEntry? rarity, int? level)
    {
        if (query.Upgrades is not { } upgrades) return null;

        var ceiling = query.MaxUpgrades
            ?? Tempering.Ceiling(store.Vocabulary.MaxTempers, rarity?.Name, level);

        return ceiling is { } max
            ? $"{upgrades.ToString(CultureInfo.InvariantCulture)} / {max.ToString(CultureInfo.InvariantCulture)}"
            : upgrades.ToString(CultureInfo.InvariantCulture);
    }

    public IReadOnlyList<Fact> Facts(EntityDocument document, TooltipQuery query)
    {
        var facts = new List<Fact>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);

            facts.Add(new Fact(label, unit is null ? text : text + unit, title));
        }

        var (cast, hastened) = Hastened(document, DocumentFields.Number(document, nameof(AbilityDocument.CastTime)), query);

        Add("Cast", cast, "s", hastened);
        Add("Channel", DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)), "s");
        Add("Cooldown", DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)), "s");
        Add("Charges", DocumentFields.Number(document, nameof(AbilityDocument.Charges)));
        Add("Range", DocumentFields.Number(document, nameof(AbilityDocument.Range)));
        Add("Radius", DocumentFields.Number(document, nameof(AbilityDocument.Radius)));
        Add("Duration", DocumentFields.Number(document, nameof(AbilityDocument.Duration)), "s");

        if (DocumentFields.Number(document, nameof(AbilityDocument.Cost)) is { } cost)
        {
            var text = Math.Round((decimal)cost, 2).Normalize().ToString(CultureInfo.InvariantCulture);
            var resource = DocumentFields.Text(document, nameof(AbilityDocument.Resource));

            facts.Add(new Fact("Cost", resource is { } r ? $"{text} {r}" : text, null));
        }

        return facts;
    }

    private static (double? Value, string? Title) Hastened(EntityDocument document, double? value, TooltipQuery query)
    {
        if (value is not { } seconds || ScalesWithHaste(document) is not true) return (value, null);
        if (query.Haste is not { } haste || haste <= 0) return (value, null);

        var text = (haste * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

        return (
            (double)((decimal)seconds / (1m + haste)),
            $"{seconds.ToString(CultureInfo.InvariantCulture)}s at no haste, " +
            $"divided by {(1m + haste).Normalize().ToString(CultureInfo.InvariantCulture)} for {text}% haste");
    }

    private static bool? ScalesWithHaste(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.ScalesWithHaste,
        RelicPowerDocument relic => relic.ScalesWithHaste,
        WeaponPowerDocument weapon => weapon.ScalesWithHaste,
        _ => null,
    };

    public string? Potential(TooltipQuery query) =>
        query.PotentialPower is { } power
            ? power.Normalize().ToString(CultureInfo.InvariantCulture)
            : null;

    public IReadOnlyList<TraitLine> Traits(TooltipQuery query)
    {
        if (query.Traits.Count == 0) return [];

        return
        [
            .. query.Traits.Select(imbued => new TraitLine(
                store.All(TraitType, imbued.Id).Select(PublicTypes.NameOf).FirstOrDefault()
                    ?? imbued.Id.ToString(CultureInfo.InvariantCulture),
                imbued.Rank)),
        ];
    }

    public IReadOnlyList<StatLine> Rolls(EntityDocument document, TooltipQuery query, RarityEntry? rarity) =>
        document is ItemDocument item
            ? StatMagnitudes.For(
                item,
                store.Vocabulary.StatCalculation,
                store.PrimaryAttributes,
                rarity,
                query.ItemLevel ?? store.DropItemLevel(item),
                query)
            : [];

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public MarkupString Text(
        string source,
        IReadOnlyList<Placeholder>? substitutions,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        new(renderer.Render(
            source,
            substitutions is not { Count: > 0 } ? null : placeholders.Resolve(Keyed(substitutions), query, hero, tags)));

    private static IReadOnlyDictionary<string, Placeholder> Keyed(IReadOnlyList<Placeholder> tokens)
    {
        var keyed = new Dictionary<string, Placeholder>(tokens.Count, StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Key is { } key) keyed[key] = token;
        }

        return keyed;
    }

    public string Style() => sheet;

    private static string Compose(
        IWebHostEnvironment environment, ChromeAssets chrome, RichTextRenderer renderer)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(Sheet);

        if (!file.Exists || file.Length == 0)
        {
            throw new InvalidOperationException(
                $"{Sheet} is missing from the web root, so the tooltip has no rules to carry. " +
                "Sass writes it from Styles/tooltip.scss on build, which pulls in " +
                "Components/Tooltip/_Tooltip.razor.scss.");
        }

        using var reading = new StreamReader(file.CreateReadStream());

        return
            $$"""
            .{{Root}}{--fx-slot-plate:url({{chrome.SlotPlate}});--fx-window:url({{chrome.Window}});
            --fx-thin:url({{chrome.ThinWindow}});--fx-fill:url({{chrome.WindowFill}});
            --fx-divider:url({{chrome.GoldDivider}});
            --fx-ability:{{chrome.AbilityBadge.Colour}};--fx-legendary:{{chrome.LegendaryBadge.Colour}};
            --fx-setbonus:{{chrome.SetBonusBadge.Colour}}}

            """ +
            reading.ReadToEnd() +
            renderer.Css(Root);
    }

    public sealed record Fact(string Label, string Value, string? Title);

    public sealed record TraitLine(string Name, int Rank);

    public enum Block { Identity, Facts, Rolls, Traits, Prose, Overflow, Granted, Legendaries, Sets }

    public IReadOnlyList<Block> Blocks(
        EntityDocument document,
        TooltipQuery query,
        RarityEntry? rarity,
        IReadOnlyList<Fact> facts,
        IReadOnlyList<StatLine> rolls,
        IReadOnlyList<TraitLine> traits)
    {
        var shown = new List<Block>(9);
        var item = document as ItemDocument;

        if (HasKind(document) || item?.Slot is not null ||
            Level(document, query) is not null || rarity is not null ||
            Temper(query, rarity, Level(document, query)) is not null ||
            item?.GemSlot == true || item?.UniqueEquipped == true) shown.Add(Block.Identity);

        if (facts.Count > 0) shown.Add(Block.Facts);

        if (Potential(query) is not null || rolls.Count > 0) shown.Add(Block.Rolls);

        if (traits.Count > 0 || query.AllocatedTraits is not null || query.MaxTraits is not null)
        {
            shown.Add(Block.Traits);
        }

        if (DocumentFields.Text(document, nameof(AbilityDocument.Description)) is not null ||
            DocumentFields.Words(document, nameof(AbilityDocument.Detail)) is { Count: > 0 }) shown.Add(Block.Prose);

        if (document is GemTypeDocument { OverflowDescription: not null }) shown.Add(Block.Overflow);

        if (Granted(document) is { } grants &&
            (grants.Name ?? DocumentFields.Text(grants, nameof(AbilityDocument.Description))) is not null)
        {
            shown.Add(Block.Granted);
        }

        if (LegendaryStats(document).Count > 0) shown.Add(Block.Legendaries);

        if (item?.SetBonus is { Count: > 0 }) shown.Add(Block.Sets);

        return shown;
    }

    private static bool HasKind(EntityDocument document) =>
        document is ItemDocument { ArmorType: not null } or GemTierDocument or GemTraitDocument or GemTypeDocument;

    public EntityDocument? Granted(EntityDocument document) =>
        document is ItemDocument { GrantsId: { } id } && Power(id) is { } found &&
        store.All(found.Type, found.Id) is { Count: > 0 } readings
            ? readings[0]
            : null;

    private (EntityType Type, int Id)? Power(int id) =>
        store.All(EntityType.RelicPower, id).Count > 0 ? (EntityType.RelicPower, id)
        : store.All(EntityType.WeaponPower, id).Count > 0 ? (EntityType.WeaponPower, id)
        : null;

    public IReadOnlyList<EntityDocument> LegendaryStats(EntityDocument document) =>
        document is ItemDocument { LegendaryStatIds: { Count: > 0 } ids }
            ? [.. ids.SelectMany(id => store.All(EntityType.LegendaryStat, id))]
            : [];
}
