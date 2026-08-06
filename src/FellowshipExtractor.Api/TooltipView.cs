using System.Globalization;
using Azure.Storage.Blobs;
using Fellowship.SDK.Documents;
using Microsoft.AspNetCore.Components;

namespace FellowshipExtractor.Api;

/// <summary>
/// Everything one tooltip draws, worked out: the rung it is read at, the palette that rung paints
/// it in, the art, the fact rows, the rolled stat lines and the rich text. The sibling of
/// <see cref="CodexView"/> for the fragment the game itself draws.
///
/// The arithmetic lives here and the emission lives in Tooltip.razor, because the two answer
/// different questions: a haste-shortened cast time is a reading of the build, and the markup it
/// lands in is a reading of the design system.
///
/// Typed to <see cref="EntityDocument"/> like every other reader in this project. <see cref="Rolls"/>
/// reaches <see cref="StatMagnitudes.For"/>, which keys a rung's extra stat slots by
/// <see cref="Item.RuleSet"/> - the loot rule set an item is rolled under, now carried on the typed
/// document alongside <see cref="Item.Rarity"/>, the different fact of the rung a rule set fixes the
/// item at. A field that lives on one document class rather than every one of them is read by
/// matching on that class, the way <see cref="CodexView"/> and <see cref="PublicEntity"/> already do.
/// </summary>
public sealed class TooltipView(
    EntityStore store,
    DesignTokens tokens,
    RichTextRenderer renderer,
    Placeholders placeholders,
    ChromeAssets chrome,
    BlobContainerClient icons)
{
    /// <summary>The class the fragment's root wears, and the scope its whole stylesheet is written under.</summary>
    public const string Root = "fx-tip";

    private const string Tempered = "Tempered";

    public ChromeAssets Chrome => chrome;

    /// <summary>
    /// The rung this reading is drawn at: the one the caller asked for, or the one the row declares.
    /// A gem reads its ladder under different words, so which ladder is asked for matters.
    ///
    /// A row's own rung is <see cref="Item.Rarity"/> or <see cref="GemTier.Rarity"/>, joined on the
    /// tier ordinal both enums carry as their member value rather than through a printed word, since
    /// the document already states the rung rather than a word to look one up by. An item states
    /// this only where a rule set fixes it, which is every legendary and nothing else, so a caller's
    /// own rarity still wins wherever they name one.
    /// </summary>
    public RarityEntry? Rarity(EntityDocument document, TooltipQuery query)
    {
        var tier = document switch
        {
            Item { Rarity: { } rung } => (int?)rung,
            GemTier gemTier => (int)gemTier.Rarity,
            _ => null,
        };

        return store.Rarity(query.Rarity, document is GemTier)
            ?? (tier is { } t ? store.Vocabulary.Rarities.FirstOrDefault(r => r.Tier == t) : null);
    }

    /// <summary>
    /// The custom properties the fragment carries on its root, so it paints itself the same whether
    /// it is drawn into the page or into a shadow root the page's own stylesheets never enter.
    /// </summary>
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

    /// <summary>The art this reading shows, which a rung can replace, and null where it ships none.</summary>
    public string? Art(EntityDocument document, RarityEntry? rarity)
    {
        var icon = rarity is not null &&
                   document is Item { IconByRarity: { } byRarity } &&
                   byRarity.GetValueOrDefault(rarity.Name) is { } art
            ? art
            : DocumentFields.Text(document, nameof(Item.Icon));

        return icon is null ? null : $"{icons.Uri}/{icon}";
    }

    public string? Ornament(RarityEntry? rarity) => rarity is null ? null : chrome.Ornament(rarity.Name);

    /// <summary>The word a rung is printed under, which a gem spells differently.</summary>
    public string Rung(EntityDocument document, RarityEntry rarity) =>
        document is GemTier ? rarity.GemDisplayName : rarity.DisplayName;

    /// <summary>
    /// The item power on the line: what the caller asked for, or the top of the ladder for anything
    /// that rolls stats, and nothing at all for a row that neither states nor rolls one.
    /// </summary>
    public int? Level(EntityDocument document, TooltipQuery query) =>
        document is Item { Stats: { Count: > 0 } }
            ? query.ItemLevel ?? StatMagnitudes.MaximumItemLevel
            : query.ItemLevel;

    /// <summary>How many temperings the caller holds, in the words the line prints them.</summary>
    public string? Temper(TooltipQuery query) =>
        query.Upgrades is not { } upgrades
            ? null
            : query.MaxUpgrades is { } max
                ? $"{Tempered} {upgrades} / {max}"
                : $"{Tempered} {upgrades}";

    /// <summary>
    /// The scalar facts this row states, in the order the game prints them, each already formatted.
    /// A title carries the arithmetic behind a number this app worked out rather than read.
    ///
    /// Every measure is read by the name it shares across the document classes that carry it -
    /// <see cref="Ability"/> is the widest of them and so is what each is named from - the same
    /// cross-cutting read <see cref="Codex"/>'s own facet measures use.
    /// </summary>
    public IReadOnlyList<Fact> Facts(EntityDocument document, TooltipQuery query)
    {
        var facts = new List<Fact>();

        void Add(string label, double? value, string? unit = null, string? title = null)
        {
            if (value is not { } number) return;

            var text = Math.Round((decimal)number, 2).Normalize().ToString(CultureInfo.InvariantCulture);

            facts.Add(new Fact(label, unit is null ? text : text + unit, title));
        }

        // Of the times an ability states, the cast is the one haste shortens. The channel runs for
        // as long as it says and lands more ticks instead, and a duration is a length rather than a
        // rate, so neither moves.
        var (cast, hastened) = Hastened(document, DocumentFields.Number(document, nameof(Ability.CastTime)), query);

        Add("Cast", cast, "s", hastened);
        Add("Channel", DocumentFields.Number(document, nameof(Ability.ChannelTime)), "s");
        Add("Cooldown", DocumentFields.Number(document, nameof(Ability.Cooldown)), "s");
        Add("Charges", DocumentFields.Number(document, nameof(Ability.Charges)));
        Add("Range", DocumentFields.Number(document, nameof(Ability.Range)));
        Add("Radius", DocumentFields.Number(document, nameof(Ability.Radius)));
        Add("Duration", DocumentFields.Number(document, nameof(Ability.Duration)), "s");

        if (DocumentFields.Number(document, nameof(Ability.Cost)) is { } cost)
        {
            var text = Math.Round((decimal)cost, 2).Normalize().ToString(CultureInfo.InvariantCulture);
            var resource = DocumentFields.Text(document, nameof(Ability.Resource));

            facts.Add(new Fact("Cost", resource is { } r ? $"{text} {r}" : text, null));
        }

        return facts;
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

    /// <summary>
    /// Whether the row states that its own times run faster with the wearer's haste. Three document
    /// classes carry the flag and nothing folds it into <see cref="DocumentFields"/>'s reflection,
    /// because a bool does not unbox through the <c>as double?</c>/<c>as string</c> casts that helper
    /// reads through.
    /// </summary>
    private static bool? ScalesWithHaste(EntityDocument document) => document switch
    {
        Ability ability => ability.ScalesWithHaste,
        RelicPower relic => relic.ScalesWithHaste,
        WeaponPower weapon => weapon.ScalesWithHaste,
        _ => null,
    };

    /// <summary>The power potential the caller asked to see, already formatted.</summary>
    public string? Potential(TooltipQuery query) =>
        query.PotentialPower is { } power
            ? power.Normalize().ToString(CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// The stat lines this reading rolls, at the level and rung it is read at. The magnitude is not
    /// in the build: a dynamic item rolls it from its level and rarity when it drops, and this is
    /// where that arithmetic happens. Only an item rolls stats, so a row of any other type rolls none.
    /// </summary>
    public IReadOnlyList<StatLine> Rolls(EntityDocument document, TooltipQuery query, RarityEntry? rarity) =>
        document is Item item
            ? StatMagnitudes.For(
                item,
                store.Vocabulary.StatCalculation,
                store.PrimaryAttributes,
                rarity,
                query.ItemLevel ?? StatMagnitudes.MaximumItemLevel,
                query)
            : [];

    /// <summary>Which of the three colours a rolled line is drawn in.</summary>
    public static string Weight(StatLine line) =>
        line.Unresolved
            ? "rolled"
            : line.OnPrimaryPool
                ? line.Primary ? "major prime" : "major"
                : "minor";

    /// <summary>A rolled magnitude as the line prints it, signed the way the game signs it.</summary>
    public static string Magnitude(StatLine line) =>
        (line.Value < 0 ? string.Empty : "+") +
        line.Value.Normalize().ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// One passage of game text with the caller's own numbers substituted in, as rendered rich text.
    /// </summary>
    public MarkupString Text(
        string source,
        IReadOnlyDictionary<string, Placeholder>? substitutions,
        TooltipQuery query,
        HeroEntry? hero,
        IReadOnlyList<string>? tags) =>
        new(renderer.Render(
            source,
            substitutions is null ? null : placeholders.Resolve(substitutions, query, hero, tags)));

    /// <summary>
    /// The fragment's whole stylesheet, carried inside it.
    ///
    /// It travels with the markup rather than sitting in the app's own stylesheet because the same
    /// fragment is painted into a shadow root that no page stylesheet enters, and is served to
    /// callers who have no page at all. Every rule is written under the root class, so the copy that
    /// lands in the page styles the fragment and nothing else.
    /// </summary>
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

    /// <summary>
    /// One scalar the tooltip states, already formatted, with the arithmetic behind it where this
    /// app worked the number out rather than reading it.
    ///
    /// Nested, because the design system has a Fact component of its own and a second one at
    /// namespace scope would be ambiguous in every component that imports both folders.
    /// </summary>
    public sealed record Fact(string Label, string Value, string? Title);

    /// <summary>The eight things a tooltip can say, in the order it says them.</summary>
    public enum Block { Identity, Facts, Rolls, Prose, Overflow, Granted, Legendaries, Sets }

    /// <summary>
    /// Which blocks this reading has anything to say in, in order.
    ///
    /// Worked out here rather than left to each block, because the separator between two blocks is
    /// markup and a component cannot be asked whether it rendered anything. One rule goes above
    /// every block but the first, so the ordering and the guards are one question and this answers
    /// it once.
    /// </summary>
    public IReadOnlyList<Block> Blocks(
        EntityDocument document,
        TooltipQuery query,
        RarityEntry? rarity,
        IReadOnlyList<Fact> facts,
        IReadOnlyList<StatLine> rolls)
    {
        var shown = new List<Block>(8);
        var item = document as Item;

        if (HasKind(document) || item?.Slot is not null ||
            Level(document, query) is not null || rarity is not null || Temper(query) is not null ||
            item?.GemSlot == true || item?.UniqueEquipped == true) shown.Add(Block.Identity);

        if (facts.Count > 0) shown.Add(Block.Facts);

        if (Potential(query) is not null || rolls.Count > 0) shown.Add(Block.Rolls);

        if (DocumentFields.Text(document, nameof(Ability.Description)) is not null ||
            DocumentFields.Words(document, nameof(Ability.Detail)) is { Count: > 0 }) shown.Add(Block.Prose);

        if (document is GemType { OverflowDescription: not null }) shown.Add(Block.Overflow);

        // A nested row is addressed by nothing and has no id to fall back on, so a granted ability
        // is named or it is drawn as "Ability" alone; one with neither is not drawn at all.
        if (Granted(document) is { } grants &&
            (grants.Name ?? DocumentFields.Text(grants, nameof(Ability.Description))) is not null)
        {
            shown.Add(Block.Granted);
        }

        if (item?.LegendaryStatIds is { Count: > 0 }) shown.Add(Block.Legendaries);

        if (item?.SetBonus is { Count: > 0 }) shown.Add(Block.Sets);

        return shown;
    }

    /// <summary>
    /// Whether the row names an armour type or a gem type, for the identity line to draw. An item
    /// states an armour type on the reading that carries one; a gem tier, a gem ladder node and a gem
    /// type's own row each name their type outright, since <see cref="GemTier.Gem"/>,
    /// <see cref="GemTrait.Gem"/> and <see cref="GemType.Gem"/> are all required.
    /// </summary>
    private static bool HasKind(EntityDocument document) =>
        document is Item { ArmorType: not null } or GemTier or GemTrait or GemType;

    /// <summary>
    /// The row this document points at for its own mechanics and text: an item's granted relic or
    /// weapon power, a trait's or a set bonus's power, or a talent slot's talent, which every slot
    /// names outright. The same resolution as <see cref="PublicEntity"/>'s own <c>Granted</c>, kept
    /// here too because that one hands back a <see cref="PublicEntity"/> already read through a
    /// <see cref="CodexView"/> rather than the document this class still needs to draw with.
    /// </summary>
    public EntityDocument? Granted(EntityDocument document)
    {
        (EntityType Type, int Id)? address = document switch
        {
            Item { GrantsId: { } id } => Power(id),
            Trait { PowerId: { } id } => (EntityType.TraitPower, id),
            SetBonus { PowerId: { } id } => (EntityType.SetBonusPower, id),
            TalentSlot slot => (EntityType.Talent, slot.TalentId),
            _ => null,
        };

        return address is { } found && store.All(found.Type, found.Id) is { Count: > 0 } readings
            ? readings[0]
            : null;
    }

    /// <summary>
    /// Which power an item's <see cref="Item.GrantsId"/> names. The id is per type rather than
    /// global, so it is tried against both the relic and the weapon powers the corpus carries.
    /// </summary>
    private (EntityType Type, int Id)? Power(int id) =>
        store.All(EntityType.RelicPower, id).Count > 0 ? (EntityType.RelicPower, id)
        : store.All(EntityType.WeaponPower, id).Count > 0 ? (EntityType.WeaponPower, id)
        : null;
}
