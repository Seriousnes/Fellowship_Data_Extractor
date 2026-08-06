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
    BlobContainerClient icons)
{
    public const string Root = "fx-tip";

    private const string Tempered = "Tempered";

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

    public string? Temper(TooltipQuery query) =>
        query.Upgrades is not { } upgrades
            ? null
            : query.MaxUpgrades is { } max
                ? $"{Tempered} {upgrades} / {max}"
                : $"{Tempered} {upgrades}";

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

    public string Style() =>
        $$"""
        .{{Root}}{display:flex;align-items:flex-start;gap:7px;box-sizing:border-box;
        width:max-content;max-width:100%;text-align:left;
        color:var(--core-text-primary);font:400 12px/1.42 "Noto Sans",system-ui,-apple-system,
        "Segoe UI",Roboto,sans-serif;font-variant-numeric:tabular-nums;
        letter-spacing:normal;word-spacing:normal;text-transform:none;text-indent:0;
        white-space:normal;text-shadow:none}
        .{{Root}} *{box-sizing:border-box;margin:0}
        .{{Root}} .slot{position:relative;flex:0 0 auto;width:48px;height:48px;padding:3px;
        background:url({{chrome.SlotPlate}}) 0 0/100% 100% no-repeat}
        .{{Root}} .slot::after{content:"";position:absolute;inset:0;pointer-events:none;
        background:var(--fx-slot) 0 0/100% 100% no-repeat}
        .{{Root}} .art{display:block;width:100%;height:100%;object-fit:contain}
        .{{Root}} .card{position:relative;flex:0 1 274px;width:274px;min-width:0;
        background:var(--core-secondary-900);
        box-shadow:inset 0 0 0 1px var(--core-secondary-600)}
        .{{Root}} .card::before{content:"";position:absolute;top:0;left:0;right:0;
        height:106px;max-height:100%;pointer-events:none;
        background:var(--fx-frame) 0 0/100% 106px no-repeat}
        .{{Root}} .head{position:relative;padding:9px 0 7px;background:var(--fx-header) 0 0/100% 100% no-repeat}
        .{{Root}} .title{margin:0 18px;font-family:ArnoPro,"Arno Pro","Iowan Old Style",
        "Palatino Linotype",Palatino,"Book Antiqua","Times New Roman",serif;font-size:15px;
        font-weight:600;line-height:1.22;letter-spacing:.13em;text-transform:uppercase;
        text-align:center;text-wrap:balance;color:var(--fx-title)}
        .{{Root}} .orn{position:absolute;top:-7px;left:50%;transform:translateX(-50%);
        width:calc(100% * 254 / 280);height:14px;pointer-events:none}
        .{{Root}} .lines{display:flex;flex-direction:column;gap:6px;padding:8px 11px 10px}
        .{{Root}} .grp{display:flex;flex-direction:column;gap:3px}
        .{{Root}} .rule{height:8px;border:0;margin:1px 0;
        background:url({{chrome.Rule}}) 0 0/100% 100% no-repeat}
        .{{Root}} .row{display:flex;justify-content:space-between;align-items:center;gap:10px}
        .{{Root}} .row>span{display:flex;align-items:center;gap:5px;min-width:0}
        .{{Root}} .gi{flex:0 0 auto;width:13px;height:13px;object-fit:contain}
        .{{Root}} .kind{color:var(--core-text-secondary)}
        .{{Root}} .k{color:var(--core-text-secondary)}
        .{{Root}} .power{color:var(--core-secondary-000)}
        .{{Root}} .rar{color:var(--fx-title)}
        .{{Root}} .pot{color:var(--core-primary-200)}
        .{{Root}} .stats{display:flex;flex-direction:column;gap:2px;list-style:none;padding:0}
        .{{Root}} .stats li{display:flex;gap:5px}
        .{{Root}} .stats .v{font-weight:600}
        .{{Root}} .stats .guaranteed{color:var(--game-stat-primary)}
        .{{Root}} .stats .random{color:var(--game-stat-secondary)}
        .{{Root}} .stats .unresolved span:last-child{font-style:italic}
        .{{Root}} .sec{display:flex;align-items:center;gap:6px;font-weight:600}
        .{{Root}} .sec .n{font-weight:400;color:var(--core-text-secondary)}
        .{{Root}} .ability{color:{{chrome.AbilityBadge.Colour}};}
        .{{Root}} .legendary{color:{{chrome.LegendaryBadge.Colour}};}
        .{{Root}} .setbonus{color:{{chrome.SetBonusBadge.Colour}};}
        .{{Root}} .nm{font-weight:600;color:var(--core-primary-200)}
        .{{Root}} .note{color:var(--core-text-secondary)}
        .{{Root}} .desc{white-space:pre-line}
        .{{Root}} .sub{font-weight:600;border-bottom:1px dotted currentColor;cursor:help}
        .{{Root}} .tok{color:var(--core-text-disabled);font-style:italic;
        border-bottom:1px dotted currentColor;cursor:help}
        .{{Root}} .rt-unstyled{color:inherit}
        {{renderer.Css(Root)}}
        """;

    public sealed record Fact(string Label, string Value, string? Title);

    public enum Block { Identity, Facts, Rolls, Prose, Overflow, Granted, Legendaries, Sets }

    public IReadOnlyList<Block> Blocks(
        Entity entity,
        TooltipQuery query,
        RarityEntry? rarity,
        IReadOnlyList<Fact> facts,
        IReadOnlyList<StatLine> rolls)
    {
        var shown = new List<Block>(8);

        if ((entity.ArmorType ?? entity.GemType) is not null || entity.Slot is not null ||
            Level(entity, query) is not null || rarity is not null || Temper(query) is not null ||
            entity.GemSlot == true || entity.UniqueEquipped == true) shown.Add(Block.Identity);

        if (facts.Count > 0) shown.Add(Block.Facts);

        if (Potential(query) is not null || rolls.Count > 0) shown.Add(Block.Rolls);

        if (entity.Description is not null || entity.Detail is { Count: > 0 }) shown.Add(Block.Prose);

        if (entity.GemOverflow is not null) shown.Add(Block.Overflow);

        if (entity.Grants is { } grants &&
            (PublicTypes.DisplayNameOf(grants) ?? grants.Description) is not null) shown.Add(Block.Granted);

        if (entity.LegendaryStats is { Count: > 0 }) shown.Add(Block.Legendaries);

        if (entity.SetBonus is { Count: > 0 }) shown.Add(Block.Sets);

        return shown;
    }
}
