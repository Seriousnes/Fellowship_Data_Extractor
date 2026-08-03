using System.Globalization;
using System.Net;
using System.Text;
using Azure.Storage.Blobs;
using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

/// <summary>
/// One entity as a self-contained, styled HTML fragment, drawn the way the game draws it.
///
/// Self-contained means the caller drops the fragment into a page and it looks right with nothing
/// else fetched but the images: the style block travels with it and every rule is scoped under one
/// class, so it cannot reach the host page. The colours are the game's own, read out of the shipped
/// design system rather than chosen, and so is the chrome: the frame, the header band, the section
/// badges and the rules between them are the client's own textures, served from the same container
/// as the icons.
///
/// Three values change per entity and are therefore custom properties on the root rather than rules
/// in the style block. A page holding eight fragments holds eight copies of an identical stylesheet,
/// and a rule naming one entity's frame in it would repaint all eight; a custom property set in the
/// root's own style attribute inherits into that fragment and no further, including into the
/// pseudo-element the frame is drawn on.
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

    /// <summary>
    /// What the game calls an item's upgrade level. ItemTooltip_State_Upgradeable is "Tempered" and
    /// its sibling is "Non Temperable", so the word the API used to print here, "Upgrade", was the
    /// field's name rather than the game's. A literal because the string tables are not part of the
    /// export the API reads: it loads entities.jsonl and vocabulary.json, and the locres is neither.
    /// </summary>
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

    /// <summary>
    /// The item's own art, in the square the client draws it in: the shipped slot plate behind it
    /// and the rung's frame over it.
    ///
    /// Beside the tooltip rather than inside it, because the client's tooltip carries no art at
    /// all. It is drawn while the cursor is over the icon, so the icon is already on screen and
    /// repeating it in the header is a thing the game never does. A page that has no cursor has to
    /// show both, so the pair sits side by side the way an item database lays them out.
    /// </summary>
    private void Art(StringBuilder html, Entity entity, RarityEntry? rarity)
    {
        // A hero's armour set ships three rungs of art and the item's rarity picks one, so the
        // picture follows the rung being drawn rather than the entity's own best. Keyed by the
        // rung's internal name, which is what the export keys it by.
        var icon = rarity is not null && entity.IconByRarity?.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : entity.Icon;

        if (icon is null) return;

        html.Append("<figure class=\"slot\"><img class=\"art\" alt=\"\" src=\"")
            .Append(Esc(Url(icon)))
            .Append("\"></figure>");
    }

    /// <summary>The rarity an entity states for itself, for a request that named none.</summary>
    private RarityEntry? Declared(Entity entity) => store.Rarity(entity.Rarity, entity.GemTier is not null);

    /// <summary>
    /// The header band: the entity's name centred in caps in the rung's colour, and under it the
    /// ornament the rung wears. The three low rungs ship no ornament and neither does an entity with
    /// no rung, so the bar is written only when there is one and the band closes on its own padding
    /// when there is not.
    /// </summary>
    private void Head(StringBuilder html, Entity entity, RarityEntry? rarity)
    {
        html.Append("<header class=\"head\">");

        html.Append("<h3 class=\"title\">").Append(Esc(entity.DisplayName ?? entity.Name ?? "?")).Append("</h3>");

        if (rarity is not null && chrome.Ornament(rarity.Name) is { } ornament)
        {
            html.Append("<img class=\"orn\" alt=\"\" src=\"").Append(Esc(ornament)).Append("\">");
        }

        html.Append("</header>");
    }

    /// <summary>
    /// Everything under the header, as a list of blocks with a rule drawn between them. A block that
    /// has nothing to say returns empty and is dropped before the rules go in, so a tooltip never
    /// carries an empty section or two rules with nothing between them.
    /// </summary>
    private void Body(StringBuilder html, Entity entity, TooltipQuery query, HeroEntry? hero, RarityEntry? rarity)
    {
        string[] blocks =
        [
            Identity(entity, query, rarity),
            Facts(entity),
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

    /// <summary>
    /// What the thing is: the material it is made of, the slot it is worn in against the power the
    /// caller's copy rolled at, and the rung against how far it has been tempered. Three rows of the
    /// same left-and-right grammar, each written only where one side or the other has something.
    /// </summary>
    private string Identity(Entity entity, TooltipQuery query, RarityEntry? rarity)
    {
        var b = new StringBuilder();

        // Armour type on an item, gem type on a gem: both answer what sort of thing this is, and no
        // entity in the export carries both.
        if ((entity.ArmorType ?? entity.GemType) is { } kind)
        {
            b.Append("<p class=\"kind\">").Append(Esc(kind)).Append("</p>");
        }

        // An item's stats are its level, so a request that named no level is answered at the
        // highest one and the header says which level the numbers below it are for. An entity with
        // no stat slots has no level to show and shows none.
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

        // A socket is the one that reads both ways. The build says whether an item's slot type can
        // roll with one; noSocket says this copy did not, and it can only ever remove a socket the
        // item could have had, never add one.
        Row(b,
            entity.GemSlot == true ? $"<span class=\"k\">{(query.NoSocket ? "No gem socket" : "Gem socket")}</span>" : null,
            entity.UniqueEquipped == true ? "<span class=\"k\">Unique Equipped</span>" : null);

        return Group(b);
    }

    /// <summary>
    /// The fixed numbers a tooltip shows in their own place: what it costs, how long it takes, how
    /// far it reaches. Two to a row, the same row grammar as the slot and power line above, so an
    /// ability's numbers read as part of the same tooltip rather than as a strip of pills bolted to
    /// it. Only the ones the entity actually has, so an item shows none of them.
    /// </summary>
    private string Facts(Entity entity)
    {
        var facts = new List<string>();

        void Add(string label, double? value, string? unit = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);
            facts.Add($"<span class=\"k\">{Esc(label)}</span><span class=\"v\">{Esc(unit is null ? text : text + unit)}</span>");
        }

        Add("Cast", entity.CastTime, "s");
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
    /// What one copy of an item came out at: the power it could still reach, then a line per stat
    /// slot with the magnitude the item's level is worth in it.
    ///
    /// The magnitudes are computed rather than asked for, because the build states every input to
    /// them. A caller who does send numbers overrides the computed ones, one value per slot of
    /// that stat in the order they arrived, which is why a stat repeats in the query:
    /// spirit=17&amp;spirit=9&amp;spirit=9 is one item with three spirit slots and not a caller
    /// sending the same thing three times.
    ///
    /// A line's colour is the stat's, not the slot's. The palette keeps game.stat-primary and
    /// game.stat-secondary for exactly the split the two pools follow, so stamina and the wearer's
    /// attribute come out white and the four secondaries cyan, which is what the game draws. A
    /// slot whose stat the item rolls when it drops has no stat to name, so it prints as a random
    /// one and still carries the magnitude, which is the part of it the item level does decide.
    /// </summary>
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
                // The palette splits stats exactly where the two pools do, white for the wearer's
                // attributes and stamina and cyan for the four secondaries, so the colour follows
                // the stat rather than the slot: an item's own stamina line reads the same as the
                // wearer's attribute line, which is what the game draws.
                var classes = line.Unresolved
                    ? "rolled"
                    : line.OnPrimaryPool
                        ? line.Primary ? "major prime" : "major"
                        : "minor";

                b.Append("<li class=\"").Append(classes).Append("\">");

                // The sign is the number's, not a decoration. A caller describing a copy of an item
                // can hand over a negative and the game writes those with one minus, not "+-5".
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

    /// <summary>What a gem type keeps giving once every node of its ladder is unlocked.</summary>
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

    /// <summary>
    /// The ability an item grants, headed by the pink badge the game marks an ability slot with and
    /// written in that badge's own colour.
    /// </summary>
    private string Granted(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        if (entity.Grants is not { } grants) return string.Empty;
        if ((grants.DisplayName ?? grants.Description) is null) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec ability\">").Append(Glyph(chrome.AbilityBadge.Url)).Append("<span>Ability");
        if (grants.DisplayName is { } name) b.Append(": ").Append(Esc(name));
        b.Append("</span></div>");

        if (grants.Description is { } text)
        {
            b.Append("<p class=\"desc\">")
                .Append(Text(text, grants.Placeholders, query, hero, grants.Tags ?? entity.Tags))
                .Append("</p>");
        }

        return Group(b);
    }

    /// <summary>
    /// A legendary's bonus stats: a counted section heading and then a named line per roll with its
    /// own sentence under it, which is the shape the game gives a counted list of rolled extras.
    /// </summary>
    private string Legendaries(Entity entity, TooltipQuery query, HeroEntry? hero)
    {
        if (entity.LegendaryStats is not { Count: > 0 } rolls) return string.Empty;

        var b = new StringBuilder();

        b.Append("<div class=\"sec legendary\">").Append(Glyph(chrome.LegendaryBadge.Url))
            .Append("<span>Legendary Stats</span><span class=\"n\">(")
            .Append(rolls.Count.ToString(CultureInfo.InvariantCulture)).Append(")</span></div>");

        foreach (var roll in rolls)
        {
            if (roll.DisplayName is { } name) b.Append("<p class=\"nm\">").Append(Esc(name)).Append("</p>");

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

    /// <summary>One left-and-right row, written only when one side or the other has something.</summary>
    private static void Row(StringBuilder html, string? left, string? right)
    {
        if (left is null && right is null) return;

        html.Append("<div class=\"row\">");
        if (left is not null) html.Append("<span>").Append(left).Append("</span>");
        if (right is not null) html.Append("<span>").Append(right).Append("</span>");
        html.Append("</div>");
    }

    /// <summary>A block, or nothing at all where the block turned out to have no content.</summary>
    private static string Group(StringBuilder content) =>
        content.Length == 0 ? string.Empty : $"<div class=\"grp\">{content}</div>";

    private static string Glyph(string url) => $"<img class=\"gi\" alt=\"\" src=\"{Esc(url)}\">";

    /// <summary>
    /// One piece of description text, with its tokens resolved. The tags come from whichever entity
    /// owns the sentence, because they are what says whether a damage number is spoken as physical
    /// or magic, and a granted ability's school is its own rather than its item's.
    /// </summary>
    private string Text(
        string source,
        IReadOnlyDictionary<string, Placeholder>? tokens,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        renderer.Render(source, tokens is null ? null : placeholders.Resolve(tokens, query, hero, tags));

    /// <summary>
    /// Where an icon lives. The container is anonymous-read at blob level, so this is a URL the
    /// reader's own browser fetches without the API in the path.
    /// </summary>
    private string Url(string icon) => $"{icons.Uri}/{icon}";

    private static string Esc(string text) => WebUtility.HtmlEncode(text);

    /// <summary>
    /// The fragment's own stylesheet, every rule under the root class so it cannot reach the page
    /// around it.
    ///
    /// The two nested border-images are the whole of the tooltip's chrome. The panel is
    /// T_Borders_WindowBackground_TooltipSimple, 256x256 with a uniform #121D28 interior at alpha
    /// 167 and a 2px warm gold edge on all four sides, so it slices at 2 with fill and gives the
    /// dark body and its border in one declaration.
    ///
    /// The rarity frame goes on a pseudo-element rather than the root because an element has one
    /// border-image and this is the second. It is sliced 7 7 0 7. Seven is read off the texture: the
    /// top rule runs from x=6 to x=263 on row 0 and the side rails start at y=6, joined by a chamfer
    /// that occupies exactly x 0..6 by y 0..6 at each top corner. Zero at the bottom is not a
    /// simplification but the shape of the art: rows 88 to 105 of the 106 are empty and the rails
    /// fade from alpha 254 at y=6 to 1 at y=87, so there is no bottom edge to slice. Leaving the
    /// bottom at zero also keeps the fade, because the left and right edge regions then span source
    /// rows 7 to 105 whole and stretch scales the gradient to the tooltip's height; a nonzero bottom
    /// would reduce those regions to a strip and repeat it into a rail of constant weight.
    ///
    /// Inset -2px on the pseudo-element puts it on the panel's border box rather than its padding
    /// box, so the frame's rails land on the panel's own edge the way they do in the client.
    ///
    /// No font is fetched. The display face is the game's, ArnoPro, named first so a reader who has
    /// it installed sees the real thing, and followed by widely installed old-style serifs of the
    /// same high stroke contrast for everyone else. ArnoPro is a commercial Adobe face and is not
    /// redistributed, here or anywhere else in the payload.
    ///
    /// Icons are sized by CSS rather than by attribute because the shipped art is not one size:
    /// 1,354 icons come in 23 different dimensions, from 70x71 to 2048x1024.
    /// </summary>
    private string Style() =>
        $$"""
        .{{Root}}{display:flex;align-items:flex-start;gap:7px;box-sizing:border-box;
        width:max-content;max-width:100%;text-align:left;
        color:var(--core-text-primary);font:400 12px/1.42 "Noto Sans",system-ui,-apple-system,
        "Segoe UI",Roboto,sans-serif;font-variant-numeric:tabular-nums}
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
        .{{Root}} .tok{color:var(--semantic-negative);font-family:ui-monospace,monospace}
        .{{Root}} .rt-unstyled{color:inherit}
        {{renderer.Css(Root)}}
        """;
}
