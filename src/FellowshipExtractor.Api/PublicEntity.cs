using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api;

/// <summary>
/// One exported row as a reader may meet it: every field of it the game itself would show, and none
/// of the fields that are the build talking to itself.
///
/// A typed document cannot be this shape outright, even though the extractor has already done the
/// harder half of the same job: a document still carries a stat slot's key rather than the word a
/// tooltip prints for it, a scaling list of raw attribute names, and a rarity spelled as the ladder
/// files it rather than as either column of it prints. This record reads a document through
/// <see cref="EntityStore"/>'s and <see cref="Codex"/>'s own translations of those, so a caller is
/// handed the same words a page draws rather than a second reading of the build.
///
/// Nested rows recurse through the same projection. A trait's power, a set bonus's power, an item's
/// granted power, a talent slot's talent and a legendary's bonus stat lines are each documents in
/// their own right now, addressable on their own route, so a projection that stopped at the top
/// level would publish through the item what it withheld from the ability it points at.
/// </summary>
public sealed record PublicEntity
{
    /// <summary>The type's route slug.</summary>
    public required string Type { get; init; }

    public required int Id { get; init; }

    /// <summary>
    /// The row's display name, or its noun and id where it has none. Never its gameplay tag, which
    /// the export never carries at all any more.
    /// </summary>
    public required string Name { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public string? ArmorType { get; init; }
    public string? PrimaryStat { get; init; }
    public IReadOnlyList<string>? Heroes { get; init; }
    public IReadOnlyList<string>? SetBonus { get; init; }
    public int? SetCount { get; init; }
    public bool? UniqueEquipped { get; init; }

    /// <summary>
    /// The damage schools the row is, Magic / Fire and Physical / Bleed, read straight off the
    /// document rather than out of a tag list: the extractor already lifts them out and spells each
    /// one the way a tooltip's damage sentence does.
    /// </summary>
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<PublicStatSlot>? Stats { get; init; }

    /// <summary>
    /// The row's own sentence, with every number in it worked out and none of the game's markup left
    /// in it.
    ///
    /// The shipped string is not this. It writes each of its numbers as a {Token} keyed by the
    /// build's name for the row behind it and wraps its emphasis in tags naming rows of the shipped
    /// style table, so ability 1122 ships "stone has a health value (&lt;/&gt;&lt;rt.heal&gt;
    /// {StoneShieldHealth}&lt;/&gt;&lt;rt.mana&gt;) equal to {AgiCoefficient} agility" where the page
    /// over it reads "equal to 862.4% of Agility". Handing that string over published both spellings
    /// and made the record disagree with the page about one row, so this is the page's reading with
    /// the styling taken off rather than the file's.
    ///
    /// What the numbers came from is not lost with the tokens: <see cref="Placeholders"/> lists each
    /// one, in the order this sentence spends them.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>The row's further lines, each read the same way as the description.</summary>
    public IReadOnlyList<string>? Detail { get; init; }

    /// <summary>The art, addressed rather than named, which is the form a caller can actually draw.</summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// The art at each rarity it differs at, keyed by the word the game prints for the rung rather
    /// than the word the build files it under.
    /// </summary>
    public IReadOnlyDictionary<string, string>? IconUrlByRarity { get; init; }

    /// <summary>The row this one's art belongs to, on the effects that ship none of their own.</summary>
    public PublicReference? ArtBorrowedFrom { get; init; }

    /// <summary>
    /// The row this one's <see cref="Name"/> belongs to, on the effects that ship none of their own.
    ///
    /// The pair is what makes the name usable rather than the name alone. Measured over the export,
    /// 1,288 of the 3,261 effects borrow their name, and 123 of those borrowed names are the single
    /// word "Attack", so a caller given the word by itself is given 123 entries called one thing with
    /// nothing to tell them apart; given the lender as well, each is the attack of a named ability.
    /// Every lender resolves - none of the 2,570 borrowed-name and borrowed-art references the export
    /// carries names a row missing from it - so the reference always addresses something a caller can
    /// fetch.
    /// </summary>
    public PublicReference? NameBorrowedFrom { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public double? Cost { get; init; }
    public string? Resource { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    /// <summary>
    /// The attributes this record's numbers grow with, in the words the game prints for them, and
    /// spelled out as words for the attributes the shipped table has no row for.
    ///
    /// Measured over the export, 3,045 rows across 11 document types carry a scaling list and name 82
    /// attributes between them; 15 of those have a row in the shipped table (Strength on 1,145 of the
    /// rows, Intellect on 486, and 13 more) and the other 67 do not. The 67 used to be dropped, which
    /// cost the list its point: a coefficient and the knob it turns are what a caller reads a record
    /// for, and a row scaling off none of the named 15 was handed an absent field rather than "Damage
    /// Scale".
    /// </summary>
    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>
    /// Where each number in the text above comes from, in the order the text spends them.
    ///
    /// A list numbered by position rather than a map keyed by the token's own name. The key was how
    /// a caller found the token in the sentence it substituted into, and the sentence no longer
    /// carries one; the key is also a spelling in its own right. So an entry is tied to its number by
    /// where it falls, on the order the document's own placeholder list already carries, which is the
    /// same order the entry page's panel numbers by.
    /// </summary>
    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public int? TalentRow { get; init; }
    public int? TalentColumn { get; init; }
    public int? TalentCost { get; init; }

    public PublicGemOverflow? GemOverflow { get; init; }
    public string? GemType { get; init; }
    public int? GemTier { get; init; }
    public int? GemPower { get; init; }
    public string? Rarity { get; init; }
    public int? GemNode { get; init; }
    public int? RequiredGemPower { get; init; }

    /// <summary>The id of the weaker node this one takes the place of, on a gem ladder's upper half.</summary>
    public int? Replaces { get; init; }

    public string? TraitCategory { get; init; }
    public int? MaxRank { get; init; }
    public int? RandomChance { get; init; }
    public IReadOnlyList<PublicRankRun>? RankRuns { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    /// <summary>
    /// The power this row points at, resolved and drawn as its own tooltip: an item's granted relic
    /// or weapon power, a trait's power, a set bonus's power, or a talent slot's talent.
    /// </summary>
    public PublicEntity? Grants { get; init; }

    public IReadOnlyList<PublicEntity>? LegendaryStats { get; init; }

    public static PublicEntity From(EntityDocument document, CodexView view)
    {
        var tokens = CodexView.PlaceholdersOf(document);
        var tags = Codex.Schools(document);

        return new PublicEntity
        {
            Type = EntityTypes.Slug(EntityTypes.TypeOf(document)),
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            Slot = (document as Item)?.Slot,
            GemSlot = (document as Item)?.GemSlot,
            ArmorType = (document as Item)?.ArmorType?.ToString(),
            PrimaryStat = (document as Item)?.PrimaryStat?.ToString(),
            Heroes = HeroesOf(document),
            SetBonus = (document as Item)?.SetBonus,
            SetCount = (document as SetBonus)?.Pieces,
            UniqueEquipped = (document as Item)?.UniqueEquipped,

            // Through the facet's own selector, so the schools a caller is given are the schools the
            // browser filters by. A second copy of the rule is a second answer to what a school is.
            Schools = Named(tags),

            Stats = Slots(document, view),
            Description = view.PlainText(DocumentFields.Text(document, nameof(Ability.Description)), tokens, tags),
            Detail = DocumentFields.Words(document, nameof(Ability.Detail)) is { Count: > 0 } lines
                ? [.. lines.Select(line => view.PlainText(line, tokens, tags)!)]
                : null,
            IconUrl = DocumentFields.Text(document, nameof(Item.Icon)) is { } icon ? view.Icon(icon) : null,
            IconUrlByRarity = Art(document, view),
            ArtBorrowedFrom = PublicReference.From((document as Effect)?.ArtBorrowedFrom, view),
            NameBorrowedFrom = PublicReference.From((document as Effect)?.NameBorrowedFrom, view),
            CastTime = DocumentFields.Number(document, nameof(Ability.CastTime)),
            ChannelTime = DocumentFields.Number(document, nameof(Ability.ChannelTime)),
            ChannelTick = DocumentFields.Number(document, nameof(Ability.ChannelTick)),
            Cooldown = DocumentFields.Number(document, nameof(Ability.Cooldown)),
            Charges = DocumentFields.Number(document, nameof(Ability.Charges)),
            Cost = DocumentFields.Number(document, nameof(Ability.Cost)),
            Resource = DocumentFields.Text(document, nameof(Ability.Resource)),
            Range = DocumentFields.Number(document, nameof(Ability.Range)),
            Radius = DocumentFields.Number(document, nameof(Ability.Radius)),
            Duration = DocumentFields.Number(document, nameof(Ability.Duration)),
            Period = DocumentFields.Number(document, nameof(Ability.Period)),
            ScalesOff = Named(view.Codex.Scaling(DocumentFields.Words(document, nameof(Ability.ScalesOff)))),
            Placeholders = Tokens(tokens, tags, view),
            TalentRow = (document as TalentSlot)?.Row,
            TalentColumn = (document as TalentSlot)?.Column,
            TalentCost = (document as TalentSlot)?.Cost,
            GemOverflow = document is Fellowship.SDK.Documents.GemType gem ? PublicGemOverflow.From(gem, view) : null,
            GemType = GemTypeName(document),
            GemTier = (document as GemTier)?.Tier,
            GemPower = (document as GemTier)?.Power,
            Rarity = view.Rarity(document),
            GemNode = (document as GemTrait)?.Node,
            RequiredGemPower = (document as GemTrait)?.RequiredPower,
            Replaces = (document as GemTrait)?.ReplacesId,
            TraitCategory = (document as Trait)?.Category,
            MaxRank = (document as Trait)?.MaxRank,
            RandomChance = (document as Trait)?.RandomChance,
            RankRuns = document is Trait { RankRuns: { } runs } ? [.. runs.Select(PublicRankRun.From)] : null,
            AllowedRoles = (document as SetBonus)?.AllowedRoles,
            AllowedAttributes = document is SetBonus { AllowedAttributes: { } attributes }
                ? [.. attributes.Select(attribute => attribute.ToString())]
                : null,
            Grants = Granted(document, view),
            LegendaryStats = document is Item { LegendaryStatIds: { Count: > 0 } ids }
                ? [.. ids.SelectMany(id => view.Store.All(EntityType.LegendaryStat, id)).Select(stat => From(stat, view))]
                : null,
        };
    }

    /// <summary>
    /// Every hero a row is for: an item's own list, or the single hero a talent slot names, folded
    /// into a list of one on the same terms <see cref="Codex"/>'s hero facet folds it - a hero's
    /// grid never shares a slot position with another hero's, so a talent slot is exactly one hero's.
    /// </summary>
    private static IReadOnlyList<string>? HeroesOf(EntityDocument document) => document switch
    {
        Item { Heroes: { Count: > 0 } list } => [.. list.Select(hero => hero.ToString())],
        TalentSlot slot => [slot.Hero.ToString()],
        _ => null,
    };

    /// <summary>
    /// The row this one points at for its own mechanics and text, resolved to its own projection: an
    /// item's granted relic or weapon power, a trait's or a set bonus's power, or a talent slot's
    /// talent, which every slot names outright.
    /// </summary>
    private static PublicEntity? Granted(EntityDocument document, CodexView view)
    {
        (EntityType Type, int Id)? address = document switch
        {
            Item { GrantsId: { } id } => Power(view.Store, id),
            Trait { PowerId: { } id } => (EntityType.TraitPower, id),
            SetBonus { PowerId: { } id } => (EntityType.SetBonusPower, id),
            TalentSlot slot => (EntityType.Talent, slot.TalentId),
            _ => null,
        };

        if (address is not { } found) return null;

        return view.Store.All(found.Type, found.Id) is { Count: > 0 } readings ? From(readings[0], view) : null;
    }

    /// <summary>
    /// Which power an item's <see cref="Item.GrantsId"/> names. The id is per type rather than
    /// global, so it is tried against both the relic and the weapon powers the corpus carries.
    /// Measured over the export, every one of the 100 items that grant something resolves to a relic
    /// power and none to a weapon power, so a caller that only ever sees a relic answer today is
    /// reading a fact about the build rather than a gap in this lookup.
    /// </summary>
    private static (EntityType Type, int Id)? Power(EntityStore store, int id) =>
        store.All(EntityType.RelicPower, id).Count > 0 ? (EntityType.RelicPower, id)
        : store.All(EntityType.WeaponPower, id).Count > 0 ? (EntityType.WeaponPower, id)
        : null;

    /// <summary>The gem type a gem tier, a gem trait or a gem type's own row belongs to.</summary>
    private static string? GemTypeName(EntityDocument document) => document switch
    {
        GemTier tier => tier.Gem.ToString(),
        GemTrait trait => trait.Gem.ToString(),
        Fellowship.SDK.Documents.GemType type => type.Gem.ToString(),
        _ => null,
    };

    /// <summary>
    /// The per-rarity art, re-keyed from the filed word to the printed one. A rung the vocabulary
    /// does not know is dropped rather than passed through, because passing it through is the leak:
    /// the only keys that could reach that branch are spellings the rarity table has no printed word
    /// for, which is exactly the case for withholding them.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? Art(EntityDocument document, CodexView view)
    {
        if (document is not Item { IconByRarity: { Count: > 0 } art }) return null;

        var printed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (rung, icon) in art)
        {
            var known = view.Store.Vocabulary.Rarities.FirstOrDefault(r =>
                string.Equals(r.Name, rung, StringComparison.OrdinalIgnoreCase));

            // An item's word rather than a gem's. Every row carrying this map is a dynamic hero
            // item, whose rungs the game prints as Heroic and Regal where a gem of the same ordinal
            // would print Flawless and Heroic.
            if (known is not null) printed[known.DisplayName] = view.Icon(icon);
        }

        return printed.Count == 0 ? null : printed;
    }

    /// <summary>
    /// The substitutions a text spends, numbered as it spends them: the document's own placeholder
    /// list, ordered at extraction, read straight through so entry 2 of a record and entry 2 of the
    /// page are the same number in the same sentence.
    /// </summary>
    private static IReadOnlyList<PublicPlaceholder>? Tokens(
        IReadOnlyList<Placeholder> tokens, IReadOnlyList<string> tags, CodexView view) =>
        tokens.Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags)).ToArray() is { Length: > 0 } spent
            ? spent
            : null;

    /// <summary>
    /// The item's stat slots with each stat under the name it is printed by, every one of them.
    ///
    /// Nothing is held back for the shipped table naming no word for a stat, on the same reading the
    /// page takes: a hidden slot understates the item's stat budget, which is the whole of what the
    /// list says. All 8 stats a slot holds in the export - Agility, CritChance, Expertise, Haste,
    /// Intellect, Spirit, Stamina and Strength - have a row and print a word, so a ninth the build
    /// added would arrive spaced out rather than leave the item a slot short, and this endpoint and
    /// the entry page would still be counting the same slots.
    /// </summary>
    private static IReadOnlyList<PublicStatSlot>? Slots(EntityDocument document, CodexView view) =>
        document is Item { Stats: { Count: > 0 } slots }
            ? [.. slots.Select(slot => new PublicStatSlot(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))]
            : null;

    /// <summary>
    /// A list only where something survived the naming, because a record left holding an empty list
    /// says the row carries none of that thing, which is a different fact from carrying only ones
    /// the game has no word for.
    /// </summary>
    internal static IReadOnlyList<string>? Named(IEnumerable<string> values) =>
        values.ToArray() is { Length: > 0 } named ? named : null;
}

/// <summary>
/// One stat an item carries and the share of its budget the slot holding it is worth, with the stat
/// under the name the game prints for it: the export files the slot on item 5202 as CritChance and
/// the same item's tooltip reads "+13 Critical Strike".
/// </summary>
public sealed record PublicStatSlot(string Stat, int Weight, string Kind);

/// <summary>
/// Another row, addressed the way a reader would ask for it again: the slug its type is reached by
/// and its id, with the printed name resolved off the store rather than carried by the reference
/// itself, which the extractor never writes.
/// </summary>
public sealed record PublicReference(string Type, int Id, string Name)
{
    public static PublicReference? From(EntityReference? source, CodexView view) =>
        source is null ? null : new PublicReference(
            EntityTypes.Slug(source.Type), source.Id, view.Name(source.Type, source.Id));
}

/// <summary>
/// What a {Token} resolves to. Everything the arithmetic needs, without the tuning rows it was
/// worked out from: a caller substituting a number into a sentence needs the number rather than the
/// provenance.
/// </summary>
public sealed record PublicPlaceholder
{
    /// <summary>
    /// Which number of the text this is, counting from the first one the sentence spends. This is
    /// the whole of a substitution's public identity: the name it is filed under is the build's, so
    /// position is what ties an entry to the number standing in the text.
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// What the entry prints as with no caller attached, which is exactly the string standing in
    /// <see cref="PublicEntity.Description"/> where the token was. A coefficient reads as the share
    /// it is, "862.4% of Agility", because that is what the game itself shows before a character is
    /// attached to the tooltip; a token the build settles nowhere reads as the marker saying so.
    /// </summary>
    public required string Reading { get; init; }

    /// <summary>
    /// The attribute the coefficient multiplies, in the game's own words, and absent on the tokens
    /// naming PrimaryAttribute. That one stands in for whichever stat the wearer's hero uses and the
    /// build ships no word for it, which is also why a tooltip asked for it without a hero prints a
    /// bare percentage and leaves the stat unsaid rather than naming it.
    ///
    /// Read through <see cref="EntityStore.StatDisplay"/> rather than through
    /// <see cref="EntityStore.StatName"/>, which is the one place in this file that distinction
    /// matters. StatName now spaces out a key the shipped table has no row for, and a token names
    /// exactly one of Strength, Intellect, Agility or the stand-in, so spacing the fourth out would
    /// fill this field with "Primary Attribute" and assert an attribute the game does not have.
    /// </summary>
    public string? Attribute { get; init; }
    public double? Coefficient { get; init; }
    public double? Spread { get; init; }
    public double? Value { get; init; }
    public string? Text { get; init; }
    public bool? Percent { get; init; }
    public string? Format { get; init; }
    public string? Unit { get; init; }
    public bool? ShowDamageType { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public static PublicPlaceholder From(
        Placeholder token, CodexView view, int ordinal, IReadOnlyList<string>? tags) => new()
    {
        Ordinal = ordinal,
        Reading = view.Reading(token, tags),
        Attribute = view.Store.StatDisplay(token.Attribute)?.DisplayName is { Length: > 0 } printed
            ? printed
            : null,
        Coefficient = token.Coefficient,
        Spread = token.Spread,
        Value = token.Value,
        Text = token.Text,
        Percent = token.Percent,
        Format = token.Format,
        Unit = token.Unit,
        ShowDamageType = token.ShowDamageType,
        ScalesOff = PublicEntity.Named(view.Codex.Scaling(token.ScalesOff)),
    };
}

/// <summary>
/// One rank progression a trait's description prints, matched to the curve behind it. The printed
/// row, the computed values and the arithmetic between them all come through; the tuning row's own
/// name does not.
/// </summary>
public sealed record PublicRankRun(string Literal, IReadOnlyList<string> Printed)
{
    public IReadOnlyList<double?>? Values { get; init; }
    public string? Transform { get; init; }
    public int? Ticks { get; init; }
    public bool? Disagrees { get; init; }

    public static PublicRankRun From(RankRun run) => new(run.Literal, run.Printed)
    {
        Values = run.Values,
        Transform = run.Transform,
        Ticks = run.Ticks,
        Disagrees = run.Disagrees,
    };
}

/// <summary>
/// What a gem type keeps giving once every node of its ladder is unlocked. The sentence and its
/// substitutions are read on the same terms as an entity's own, because it is a description of the
/// same shape and the page over it already draws it through the same renderer.
///
/// A gem type carries no tags of its own, so the sentence speaks no damage school - matching the six
/// gem types themselves, none of which is a school-carrying document type.
/// </summary>
public sealed record PublicGemOverflow(string Description)
{
    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public string? IconUrl { get; init; }

    public static PublicGemOverflow? From(Fellowship.SDK.Documents.GemType gem, CodexView view)
    {
        if (gem.OverflowDescription is not { } description) return null;

        var tokens = gem.OverflowPlaceholders ?? [];

        return new PublicGemOverflow(view.PlainText(description, tokens, [])!)
        {
            Placeholders = tokens.Select((token, at) => PublicPlaceholder.From(token, view, at + 1, [])).ToArray()
                is { Length: > 0 } spent
                ? spent
                : null,
            IconUrl = gem.Icon is null ? null : view.Icon(gem.Icon),
        };
    }
}
