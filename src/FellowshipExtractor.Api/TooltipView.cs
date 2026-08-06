using System.Globalization;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;
using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api;

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

    public const string TraitSlug = "traits";

    private const string Sheet = "css/tooltip.css";

    private readonly string sheet = Compose(environment, chrome, renderer);

    public ChromeAssets Chrome => chrome;

    public RarityEntry? Rarity(Entity entity, TooltipQuery query) =>
        store.Rarity(query.Rarity, entity.GemTier is not null)
        ?? store.Rarity(entity.Rarity, entity.GemTier is not null);

    public string Variables(RarityEntry? rarity)
    {
        var colour = rarity is null ? null : tokens.Rarity(rarity.Name);

        return
            tokens.CssVariables() +
            "--fx-frame:" + (chrome.Frame(rarity?.Name) is { } frame ? $"url({frame})" : "none") +
            ";--fx-slot:" + (chrome.IconFrame(rarity?.Name) is { } slot ? $"url({slot})" : "none") +
            ";--fx-header:url(" + chrome.Header(rarity?.Name) +
            ");--fx-title:" + (colour ?? "var(--core-text-primary)") +
            ";";
    }

    public string? Art(Entity entity, RarityEntry? rarity)
    {
        var icon = rarity is not null && entity.IconByRarity?.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : entity.Icon;

        return icon is null ? null : $"{icons.Uri}/{icon}";
    }

    public string? Ornament(RarityEntry? rarity) => rarity is null ? null : chrome.Ornament(rarity.Name);

    public string Rung(Entity entity, RarityEntry rarity) =>
        entity.GemTier is not null ? rarity.GemDisplayName : rarity.DisplayName;

    public int? Level(Entity entity, TooltipQuery query) =>
        entity.Stats is { Count: > 0 }
            ? query.ItemLevel ?? StatMagnitudes.MaximumItemLevel
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

    public IReadOnlyList<Fact> Facts(Entity entity, TooltipQuery query)
    {
        var facts = new List<Fact>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);

            facts.Add(new Fact(label, unit is null ? text : text + unit, title));
        }

        var (cast, hastened) = Hastened(entity, entity.CastTime, query);

        Add("Cast", cast, "s", hastened);
        Add("Channel", entity.ChannelTime, "s");
        Add("Cooldown", entity.Cooldown, "s");
        Add("Charges", entity.Charges);
        Add("Range", entity.Range);
        Add("Radius", entity.Radius);
        Add("Duration", entity.Duration, "s");

        if (entity.Cost is { } cost)
        {
            var text = Math.Round((decimal)cost, 2).Normalize().ToString(CultureInfo.InvariantCulture);

            facts.Add(new Fact("Cost", entity.Resource is { } resource ? $"{text} {resource}" : text, null));
        }

        return facts;
    }

    private static (double? Value, string? Title) Hastened(Entity entity, double? value, TooltipQuery query)
    {
        if (value is not { } seconds || entity.ScalesWithHaste is not true) return (value, null);
        if (query.Haste is not { } haste || haste <= 0) return (value, null);

        var text = (haste * 100m).Normalize().ToString(CultureInfo.InvariantCulture);

        return (
            (double)((decimal)seconds / (1m + haste)),
            $"{seconds.ToString(CultureInfo.InvariantCulture)}s at no haste, " +
            $"divided by {(1m + haste).Normalize().ToString(CultureInfo.InvariantCulture)} for {text}% haste");
    }

    public string? Potential(TooltipQuery query) =>
        query.PotentialPower is { } power
            ? power.Normalize().ToString(CultureInfo.InvariantCulture)
            : null;

    public IReadOnlyList<TraitLine> Traits(TooltipQuery query)
    {
        if (query.Traits.Count == 0) return [];

        var table = PublicTypes.TableOf(TraitSlug)!;

        return
        [
            .. query.Traits.Select(imbued => new TraitLine(
                store.All(table, imbued.NetId).Select(PublicTypes.NameOf).FirstOrDefault()
                    ?? imbued.NetId.ToString(CultureInfo.InvariantCulture),
                imbued.Rank)),
        ];
    }

    public IReadOnlyList<StatLine> Rolls(Entity entity, TooltipQuery query, RarityEntry? rarity) =>
        StatMagnitudes.For(
            entity,
            store.Vocabulary.StatCalculation,
            store.PrimaryAttributes,
            rarity,
            query.ItemLevel ?? StatMagnitudes.MaximumItemLevel,
            query);

    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    public MarkupString Text(
        string source,
        IReadOnlyDictionary<string, Placeholder>? substitutions,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        new(renderer.Render(
            source,
            substitutions is null ? null : placeholders.Resolve(substitutions, query, hero, tags)));

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
        Entity entity,
        TooltipQuery query,
        RarityEntry? rarity,
        IReadOnlyList<Fact> facts,
        IReadOnlyList<StatLine> rolls,
        IReadOnlyList<TraitLine> traits)
    {
        var shown = new List<Block>(9);

        if ((entity.ArmorType ?? entity.GemType) is not null || entity.Slot is not null ||
            Level(entity, query) is not null || rarity is not null ||
            Temper(query, rarity, Level(entity, query)) is not null ||
            entity.GemSlot == true || entity.UniqueEquipped == true) shown.Add(Block.Identity);

        if (facts.Count > 0) shown.Add(Block.Facts);

        if (Potential(query) is not null || rolls.Count > 0) shown.Add(Block.Rolls);

        if (traits.Count > 0 || query.AllocatedTraits is not null || query.MaxTraits is not null)
        {
            shown.Add(Block.Traits);
        }

        if (entity.Description is not null || entity.Detail is { Count: > 0 }) shown.Add(Block.Prose);

        if (entity.GemOverflow is not null) shown.Add(Block.Overflow);

        if (entity.Grants is { } grants &&
            (PublicTypes.DisplayNameOf(grants) ?? grants.Description) is not null) shown.Add(Block.Granted);

        if (entity.LegendaryStats is { Count: > 0 }) shown.Add(Block.Legendaries);

        if (entity.SetBonus is { Count: > 0 }) shown.Add(Block.Sets);

        return shown;
    }
}
