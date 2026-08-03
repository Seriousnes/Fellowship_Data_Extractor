using System.Globalization;
using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// One entity as a self-contained, styled HTML fragment, drawn the way the game draws it.
/// </summary>
public sealed class TooltipRenderer(
    EntityStore store,
    DesignTokens tokens,
    RichTextRenderer renderer,
    Placeholders placeholders,
    ChromeAssets chrome,
    BlobContainerClient icons)
{
    private const string Root = "fx-tip";

    private const string Tempered = "Tempered";

    public string Render(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        var rarity = store.Rarity(query.Rarity, entity.GemTier is not null) ?? Declared(entity);
        var colour = rarity is null ? null : tokens.Rarity(rarity.Name);

        var html = new StringBuilder(4096);

        html.Append("<div class=\"").Append(Root).Append("\" style=\"").Append(tokens.CssVariables())
            .Append("--fx-frame:").Append(chrome.Frame(rarity?.Name) is { } frame ? $"url({Esc(frame)})" : "none")
            .Append(";--fx-slot:")
            .Append(chrome.IconFrame(rarity?.Name) is { } slot ? $"url({Esc(slot)})" : "none")
            .Append(";--fx-header:url(").Append(Esc(chrome.Header(rarity?.Name)))
            .Append(");--fx-title:").Append(colour ?? "var(--core-text-primary)")
            .Append(";\">");

        html.Append("<style>").Append(Style()).Append("</style>");

        Art(html, entity, rarity);

        html.Append("<div class=\"card\">");
        Head(html, entity, rarity);
        Body(html, entity, query, hero, rarity);
        html.Append("</div></div>");

        return html.ToString();
    }

    private void Art(StringBuilder html, Entity entity, RarityEntry? rarity)
    {
        var icon = rarity is not null && entity.IconByRarity?.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : entity.Icon;

        if (icon is null) return;

        html.Append("<figure class=\"slot\"><img class=\"art\" alt=\"\" src=\"")
            .Append(Esc(Url(icon)))
            .Append("\"></figure>");
    }

    private RarityEntry? Declared(Entity entity) => store.Rarity(entity.Rarity, entity.GemTier is not null);

    private void Head(StringBuilder html, Entity entity, RarityEntry? rarity)
    {
        html.Append("<header class=\"head\">");

        // The row's public name, which for more than half of them is "Effect 2018" rather than a
        // display name. Falling back to the gameplay tag put the build's own spelling in the one
        // place a tooltip is certain to be read, on 7,957 of the 14,478 rows a reader can reach.
        html.Append("<h3 class=\"title\">").Append(Esc(PublicTypes.NameOf(entity))).Append("</h3>");

        if (rarity is not null && chrome.Ornament(rarity.Name) is { } ornament)
        {
            html.Append("<img class=\"orn\" alt=\"\" src=\"").Append(Esc(ornament)).Append("\">");
        }

        html.Append("</header>");
    }

    private void Body(StringBuilder html, Entity entity, TooltipQuery query, HeroEntry? hero, RarityEntry? rarity)
    {
        string[] blocks =
        [
            Identity(entity, query, rarity),
            Facts(entity, query),
            Rolls(entity, query, rarity),
            Prose(entity, query, hero),
            Overflow(entity, query, hero),
            Granted(entity, query, hero),
            Legendaries(entity, query, hero),
            Sets(entity),
        ];

        var written = blocks.Where(block => block.Length > 0).ToArray();
        if (written.Length == 0) return;

        html.Append("<div class=\"lines\">")
            .Append(string.Join("<hr class=\"rule\">", written))
            .Append("</div>");
    }

    private string Identity(Entity entity, TooltipQuery query, RarityEntry? rarity)
    {
        var b = new StringBuilder();

        if ((entity.ArmorType ?? entity.GemType) is { } kind)
        {
            b.Append("<p class=\"kind\">").Append(Esc(kind)).Append("</p>");
        }

        var level = entity.Stats is { Count: > 0 }
            ? query.ItemLevel ?? StatMagnitudes.MaximumItemLevel
            : query.ItemLevel;

        Row(b,
            entity.Slot is { } slot ? Glyph(chrome.SlotGlyph) + Esc(slot) : null,
            level is { } power
                ? Glyph(chrome.ItemPowerGlyph) + $"<span class=\"power\">{power.ToString(CultureInfo.InvariantCulture)}</span>"
                : null);

        Row(b,
            rarity is null
                ? null
                : $"<span class=\"rar\">{Esc(entity.GemTier is not null ? rarity.GemDisplayName : rarity.DisplayName)}</span>",
            query.Upgrades is { } upgrades
                ? Esc(query.MaxUpgrades is { } max
                    ? $"{Tempered} {upgrades} / {max}"
                    : $"{Tempered} {upgrades}")
                : null);

        Row(b,
            entity.GemSlot == true ? $"<span class=\"k\">{(query.NoSocket ? "No gem socket" : "Gem socket")}</span>" : null,
            entity.UniqueEquipped == true ? "<span class=\"k\">Unique Equipped</span>" : null);

        return Group(b);
    }

    private string Facts(Entity entity, TooltipQuery query)
    {
        var facts = new List<string>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);
            var explained = title is null ? "" : $" title=\"{Esc(title)}\"";
            facts.Add(
                $"<span class=\"k\">{Esc(label)}</span>" +
                $"<span class=\"v\"{explained}>{Esc(unit is null ? text : text + unit)}</span>");
        }

        // Of the times an ability states, the cast is the one haste shortens. The channel runs for
        // as long as it says and lands more ticks instead, and a duration is a length rather than a
        // rate, so neither moves.
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
            facts.Add($"<span class=\"k\">Cost</span><span class=\"v\">{Esc(entity.Resource is { } resource ? $"{text} {resource}" : text)}</span>");
        }

        var b = new StringBuilder();
        for (var at = 0; at < facts.Count; at += 2)
        {
            Row(b, facts[at], at + 1 < facts.Count ? facts[at + 1] : null);
        }

        return Group(b);
    }

    /// <summary>
    /// A cast time shortened by the caller's haste, and the sentence explaining it.
    ///
    /// Gated on the ability's own ScaleTimeWithHaste, which is what that flag is for: it separates
    /// the abilities a wearer's haste reaches from the ones it does not. The separation is clean in
    /// the shipped data - of the 224 abilities that state a cast time without the flag, 208 belong
    /// to NPCs and bosses, 15 are never shown, and one is a hero's.
    ///
    /// The arithmetic is the same reading applied to a tickrate and is equally the reader's: the
    /// build says which times haste reaches and never by how much. The title carries the number
    /// before it so the figure is never mistaken for one the export stated.
    /// </summary>
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

    private string Rolls(Entity entity, TooltipQuery query, RarityEntry? rarity)
    {
        var b = new StringBuilder();

        if (query.PotentialPower is { } power)
        {
            Row(b,
                "<span class=\"k\">Power Potential</span>",
                $"<span class=\"pot\">{Esc(power.Normalize().ToString(CultureInfo.InvariantCulture))}</span>");
        }

        var lines = StatMagnitudes.For(
            entity,
            store.Vocabulary.StatCalculation,
            store.PrimaryAttributes,
            rarity,
            query.ItemLevel ?? StatMagnitudes.MaximumItemLevel,
            query);

        if (lines.Count > 0)
        {
            b.Append("<ul class=\"stats\">");

            foreach (var line in lines)
            {
                var classes = line.Unresolved
                    ? "rolled"
                    : line.OnPrimaryPool
                        ? line.Primary ? "major prime" : "major"
                        : "minor";

                b.Append("<li class=\"").Append(classes).Append("\">");

                b.Append("<span class=\"v\">")
                    .Append(line.Value < 0 ? string.Empty : "+")
                    .Append(Esc(line.Value.Normalize().ToString(CultureInfo.InvariantCulture)))
                    .Append("</span>");

                b.Append("<span>").Append(Esc(line.DisplayName)).Append("</span></li>");
            }

            b.Append("</ul>");
        }

        return Group(b);
    }

    private string Prose(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        var b = new StringBuilder();

        if (entity.Description is { } description)
        {
            b.Append("<p class=\"desc\">")
                .Append(Text(description, entity.Placeholders, query, hero, entity.Tags))
                .Append("</p>");
        }

        foreach (var detail in entity.Detail ?? [])
        {
            b.Append("<p class=\"note\">")
                .Append(Text(detail, entity.Placeholders, query, hero, entity.Tags))
                .Append("</p>");
        }

        return Group(b);
    }

    private string Overflow(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        if (entity.GemOverflow is not { } overflow) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec\"><span>Past Max Power</span></div>")
            .Append("<p class=\"desc\">")
            .Append(Text(overflow.Description, overflow.Placeholders, query, hero, entity.Tags))
            .Append("</p>");

        return Group(b);
    }

    private string Granted(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        if (entity.Grants is not { } grants) return string.Empty;

        // The granted ability's own name, on the same terms as any other: a nested row is addressed
        // by nothing and has no id to fall back on, so it is named or it is drawn as "Ability" alone.
        var named = PublicTypes.DisplayNameOf(grants);

        if ((named ?? grants.Description) is null) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec ability\">").Append(Glyph(chrome.AbilityBadge.Url)).Append("<span>Ability");
        if (named is { } name) b.Append(": ").Append(Esc(name));
        b.Append("</span></div>");

        if (grants.Description is { } text)
        {
            b.Append("<p class=\"desc\">")
                .Append(Text(text, grants.Placeholders, query, hero, grants.Tags ?? entity.Tags))
                .Append("</p>");
        }

        return Group(b);
    }

    private string Legendaries(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        if (entity.LegendaryStats is not { Count: > 0 } rolls) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec legendary\">").Append(Glyph(chrome.LegendaryBadge.Url))
            .Append("<span>Legendary Stats</span><span class=\"n\">(")
            .Append(rolls.Count.ToString(CultureInfo.InvariantCulture)).Append(")</span></div>");

        foreach (var roll in rolls)
        {
            if (PublicTypes.DisplayNameOf(roll) is { } name)
            {
                b.Append("<p class=\"nm\">").Append(Esc(name)).Append("</p>");
            }

            if (roll.Description is { } text)
            {
                b.Append("<p class=\"desc\">")
                    .Append(Text(text, roll.Placeholders, query, hero, roll.Tags ?? entity.Tags))
                    .Append("</p>");
            }
        }

        return Group(b);
    }

    private string Sets(Entity entity)
    {
        if (entity.SetBonus is not { Count: > 0 } sets) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec setbonus\">").Append(Glyph(chrome.SetBonusBadge.Url))
            .Append("<span>Set Bonus</span>");

        if (entity.SetCount is { } count)
        {
            b.Append("<span class=\"n\">(").Append(count.ToString(CultureInfo.InvariantCulture)).Append(" pieces)</span>");
        }

        b.Append("</div><p class=\"desc\">").Append(Esc(string.Join(", ", sets))).Append("</p>");

        return Group(b);
    }

    private static void Row(StringBuilder html, string? left, string? right)
    {
        if (left is null && right is null) return;

        html.Append("<div class=\"row\">");
        if (left is not null) html.Append("<span>").Append(left).Append("</span>");
        if (right is not null) html.Append("<span>").Append(right).Append("</span>");
        html.Append("</div>");
    }

    private static string Group(StringBuilder content) =>
        content.Length == 0 ? string.Empty : $"<div class=\"grp\">{content}</div>";

    private static string Glyph(string url) => $"<img class=\"gi\" alt=\"\" src=\"{Esc(url)}\">";

    private string Text(
        string source,
        IReadOnlyDictionary<string, Placeholder>? tokens,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        renderer.Render(source, tokens is null ? null : placeholders.Resolve(tokens, query, hero, tags));

    private string Url(string icon) => $"{icons.Uri}/{icon}";

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    private string Style() =>
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
        border:2px solid transparent;border-image:url({{chrome.Panel}}) 2 fill stretch}
        .{{Root}} .card::before{content:"";position:absolute;inset:-2px;box-sizing:border-box;
        pointer-events:none;border:7px solid transparent;border-bottom-width:0;
        border-image-source:var(--fx-frame);border-image-slice:7 7 0 7;border-image-repeat:stretch}
        .{{Root}} .head{position:relative;padding:9px 0 7px;background:var(--fx-header) 0 0/100% 100% no-repeat}
        .{{Root}} .title{margin:0 18px;font-family:ArnoPro,"Arno Pro","Iowan Old Style",
        "Palatino Linotype",Palatino,"Book Antiqua","Times New Roman",serif;font-size:15px;
        font-weight:600;line-height:1.22;letter-spacing:.13em;text-transform:uppercase;
        text-align:center;text-wrap:balance;color:var(--fx-title)}
        .{{Root}} .orn{display:block;width:100%;height:28px;margin-top:2px}
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
        .{{Root}} .stats .major,.{{Root}} .stats .major .v{color:var(--game-stat-primary)}
        .{{Root}} .stats .minor,.{{Root}} .stats .minor .v{color:var(--game-stat-secondary)}
        .{{Root}} .stats .rolled,.{{Root}} .stats .rolled .v{color:var(--core-text-disabled)}
        .{{Root}} .stats .rolled span:last-child{font-style:italic}
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
}
