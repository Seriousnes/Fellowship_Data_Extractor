namespace FellowshipExtractor.Api;

/// <summary>
/// One exported record as a reader may meet it: every field of it the game itself would show, and
/// none of the fields that are the build talking to itself.
///
/// <see cref="Entity"/> cannot be this shape. It is the record the extractor serialises
/// <c>entities.jsonl</c> from, so a field dropped there is a field dropped from the export, and the
/// export is meant to carry everything. The two readings differ, and this is the reader's one.
///
/// What is left out is left out because the export's own census says nobody was ever shown it. The
/// gameplay tag every one of the 15,235 lines carries is the build's name for the row and the thing
/// the ruling names outright. <c>constants</c> is on 3,239 rows keyed by shipped tuning paths, and
/// those keys carry the codenames the roster does not - Vigour's rows are filed under Vigor - so
/// publishing the map publishes the codename. <c>talents</c> is keyed the same way, <c>talentGates</c>
/// and <c>grantedTags</c> are gameplay tags outright, <c>talentSlot</c> is spelled Talent.ID.Talent15,
/// <c>ruleSet</c> is Default_NoGemSlot_Necklace, <c>traitCategoryTag</c> is ItemTrait.TraitCategory.Heroic
/// beside a <c>traitCategory</c> already carrying the printed word, and a rank run's <c>key</c> is
/// Trait.AbilityToIncreasedMainStat.Buff.AdditionalMainStat. <c>gemAttribute</c> joins them: it is
/// AmethystPower on 6 rows, an attribute key the shipped table has no printed word for, and each of
/// those 6 rows is already named Amethyst outright.
///
/// Eleven fields are rewritten rather than dropped, because the fact under them is public and only
/// the spelling is not. A row's tag list is 40-odd internal classifications around the damage
/// schools, which are the one family of tags a player reads off a tooltip, so the schools come
/// through, in the spelling a page prints rather than the one the tag is filed under, and the rest
/// does not. <c>iconByRarity</c> is keyed by the word the build files a rung under, which is both
/// unshown and actively wrong to show: rung 4 is filed Champion and prints Heroic, and rung 5 is
/// filed Heroic and prints Regal, so a caller reading those keys reads one rung as another. And
/// <c>iconInheritedFrom</c> and <c>nameInheritedFrom</c> address another row by table and gameplay
/// tag, which is that row's public address written the one way it must not be, so each is answered
/// with the slug, the id and the printed name instead.
///
/// The other three carry an attribute name, which the export always writes the build's way, and each
/// is read through the shipped attribute table so that what a caller is given is the word the game
/// prints. A stat slot's <c>stat</c> is CritChance on the item whose tooltip reads "+13 Critical
/// Strike", and all 8 the export uses have a row. <c>scalesOff</c> is 82 distinct names of which the
/// table has a word for 15; the other 67 are tuning knobs rather than stats anybody wears -
/// DamageScale, IncomingDamageMultiplier - and they are spelled out as words rather than dropped,
/// because the knob behind a number is data a caller came here for and only its run-together
/// spelling had to go. A placeholder's <c>attribute</c> is the one field that still drops a value:
/// the 244 tokens carrying one name four attributes between them, and 117 of those name
/// PrimaryAttribute, which is not an attribute at all but a stand-in for whichever stat the wearer's
/// hero uses, so naming it would assert a stat the game does not have.
///
/// The last four are the row's own words. <c>description</c>, each line of <c>detail</c> and a gem
/// overflow's sentence ship with every number written as a {Token} named after the tuning row behind
/// it and every emphasis wrapped in a tag naming a row of the shipped style table, so the file's
/// string carries two internal spellings and reads as neither prose nor data. Each is answered with
/// the reading the page over it draws: the numbers worked out, the tags taken off. <c>placeholders</c>
/// follows them from a map keyed by the token's own name to a list numbered by the order the sentence
/// spends them, because the key was only ever a way of finding the token in a text that no longer
/// contains one.
///
/// Nested rows recurse through the same projection. A granted ability and a legendary stat line are
/// entities in their own right and carry their own placeholders and tags, so a projection that
/// stopped at the top level would publish through the item what it withheld from the ability.
/// </summary>
public sealed record PublicEntity
{
    /// <summary>
    /// The type's route slug, and null on a nested row. Neither of the 200 granted abilities nor the
    /// 111 legendary stat lines in the export carries a table or an id at all: they are read through
    /// the row that holds them and are addressable only as part of it.
    /// </summary>
    public string? Type { get; init; }

    public int? NetId { get; init; }

    /// <summary>
    /// The row's display name, or its noun and id where it has none. Never its gameplay tag, which
    /// is what the export's own <c>name</c> holds and is the field this one replaces.
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
    /// The damage schools the row is, Magic / Fire and Physical / Bleed, read straight off the game's
    /// own damage sentences and spelled as a page prints them rather than as the tag is filed. This is
    /// the part of the export's tag list a player has met; the rest of it is Skill.Secondary and
    /// TrackCooldown and is published nowhere.
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

    /// <summary>The row this one's art belongs to, on the 1,282 records that ship none of their own.</summary>
    public PublicReference? ArtBorrowedFrom { get; init; }

    /// <summary>
    /// The row this one's <see cref="Name"/> belongs to, on the 1,288 records that ship none of
    /// their own.
    ///
    /// The pair is what makes the name usable rather than the name alone. 123 of the borrowed names
    /// are the single word "Attack" and 33 more are "Auto Attack", so a caller given the word by
    /// itself is given 123 entries called one thing with nothing to tell them apart; given the
    /// lender as well, each is the attack of a named ability. Every one of the 1,288 lenders is a
    /// row of a public table, so the reference always addresses something a caller can fetch.
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
    /// spelled out as words for the two thirds the shipped attribute table has no row for.
    ///
    /// 3,081 public rows carry one of these lists and they name 82 attributes between them. The 67
    /// with no row used to be dropped, which cost the list its point: a coefficient and the knob it
    /// turns are what a caller reads a record for, and a row scaling off none of the named 15 was
    /// handed an absent field rather than "Damage Scale".
    /// </summary>
    public IReadOnlyList<string>? ScalesOff { get; init; }

    /// <summary>
    /// Where each number in the text above comes from, in the order the text spends them.
    ///
    /// A list numbered by position rather than a map keyed by the token's own name. The key was how
    /// a caller found the token in the sentence it substituted into, and the sentence no longer
    /// carries one; the key is also a spelling in its own right, since 221 of the 237 distinct keys
    /// in the export read like StrCoeff50 and HemoStrikeTickDmg. So an entry is tied to its number by
    /// where it falls, which is the same tie the entry page's own panel draws.
    /// </summary>
    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public int? TalentRow { get; init; }
    public int? TalentColumn { get; init; }
    public int? TalentCost { get; init; }

    /// <summary>
    /// The id of the talent carrying this row's mechanics and text, on the items and traits whose
    /// content lives there. Named for where it leads, under the same word the type is addressed by.
    /// </summary>
    public int? Talent { get; init; }

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
    public PublicEntity? Grants { get; init; }
    public IReadOnlyList<PublicEntity>? LegendaryStats { get; init; }

    public static PublicEntity From(Entity entity, CodexView view) => new()
    {
        Type = PublicTypes.SlugOf(entity.Table),
        NetId = entity.NetId,
        Name = PublicTypes.NameOf(entity),
        Slot = entity.Slot,
        GemSlot = entity.GemSlot,
        ArmorType = entity.ArmorType,
        PrimaryStat = entity.PrimaryStat,
        Heroes = entity.Heroes,
        SetBonus = entity.SetBonus,
        SetCount = entity.SetCount,
        UniqueEquipped = entity.UniqueEquipped,

        // Through the facet's own selector, so the schools a caller is given are the schools the
        // browser filters by. A second copy of the rule is a second answer to what a school is.
        Schools = Codex.Schools(entity).ToArray() is { Length: > 0 } schools ? schools : null,

        Stats = Slots(entity, view),
        Description = view.PlainText(entity.Description, entity.Placeholders, entity.Tags),
        Detail = entity.Detail is not { Count: > 0 } lines
            ? null
            : [.. lines.Select(line => view.PlainText(line, entity.Placeholders, entity.Tags)!)],
        IconUrl = entity.Icon is null ? null : view.Icon(entity.Icon),
        IconUrlByRarity = Art(entity, view),
        ArtBorrowedFrom = PublicReference.From(entity.IconInheritedFrom, view),
        NameBorrowedFrom = PublicReference.From(entity.NameInheritedFrom, view),
        CastTime = entity.CastTime,
        ChannelTime = entity.ChannelTime,
        ChannelTick = entity.ChannelTick,
        Cooldown = entity.Cooldown,
        Charges = entity.Charges,
        Cost = entity.Cost,
        Resource = entity.Resource,
        Range = entity.Range,
        Radius = entity.Radius,
        Duration = entity.Duration,
        Period = entity.Period,
        ScalesOff = Named(view.Codex.Scaling(entity.ScalesOff)),
        Placeholders = Tokens(CodexView.Spends(entity), entity.Placeholders, entity.Tags, view),
        TalentRow = entity.TalentRow,
        TalentColumn = entity.TalentColumn,
        TalentCost = entity.TalentCost,
        Talent = entity.Augmentation,
        GemOverflow = PublicGemOverflow.From(entity.GemOverflow, entity.Tags, view),
        GemType = entity.GemType,
        GemTier = entity.GemTier,
        GemPower = entity.GemPower,
        Rarity = entity.Rarity,
        GemNode = entity.GemNode,
        RequiredGemPower = entity.RequiredGemPower,
        Replaces = entity.Replaces,
        TraitCategory = entity.TraitCategory,
        MaxRank = entity.MaxRank,
        RandomChance = entity.RandomChance,
        RankRuns = entity.RankRuns is null ? null : [.. entity.RankRuns.Select(PublicRankRun.From)],
        AllowedRoles = entity.AllowedRoles,
        AllowedAttributes = entity.AllowedAttributes,
        Grants = entity.Grants is null ? null : From(entity.Grants, view),
        LegendaryStats = entity.LegendaryStats is null
            ? null
            : [.. entity.LegendaryStats.Select(roll => From(roll, view))],
    };

    /// <summary>
    /// The per-rarity art, re-keyed from the filed word to the printed one. A rung the vocabulary
    /// does not know is dropped rather than passed through, because passing it through is the leak:
    /// the only keys that could reach that branch are spellings the rarity table has no printed word
    /// for, which is exactly the case for withholding them.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? Art(Entity entity, CodexView view)
    {
        if (entity.IconByRarity is not { Count: > 0 } art) return null;

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
    /// The substitutions a text spends, numbered as it spends them. Ordered through the view's own
    /// reading of that, which is what the entry page's panel numbers by, so entry 2 of a record and
    /// entry 2 of the page are the same number in the same sentence.
    /// </summary>
    private static IReadOnlyList<PublicPlaceholder>? Tokens(
        string text,
        IReadOnlyDictionary<string, Placeholder>? tokens,
        IReadOnlyList<string>? tags,
        CodexView view) =>
        view.Spent(text, tokens)
            .Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags))
            .ToArray() is { Length: > 0 } spent
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
    private static IReadOnlyList<PublicStatSlot>? Slots(Entity entity, CodexView view) =>
        entity.Stats is not { Count: > 0 } slots
            ? null
            : [.. slots.Select(slot =>
                new PublicStatSlot(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))];

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
/// and its id. The export writes the gameplay tag here as well, so a reference can be judged without
/// a second lookup, and that is the one part of it that cannot be repeated.
/// </summary>
public sealed record PublicReference(string? Type, int NetId, string Name)
{
    public static PublicReference? From(IconSource? source, CodexView view) =>
        source is null ? null : new PublicReference(
            PublicTypes.SlugOf(source.Table), source.NetId, view.Name(source.Table, source.NetId));
}

/// <summary>
/// What a {Token} resolves to. Everything the arithmetic needs, without the tuning rows it was
/// worked out from: <see cref="Placeholder.Constants"/> is on 304 of the 538 tokens this converts
/// and is keyed by shipped row paths, and a caller substituting a number into a sentence needs the
/// number rather than the provenance.
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
    /// The attribute the coefficient multiplies, in the game's own words, and absent on the 117
    /// tokens naming PrimaryAttribute. That one stands in for whichever stat the wearer's hero uses
    /// and the build ships no word for it, which is also why a tooltip asked for it without a hero
    /// prints "584.82%" and leaves the stat unsaid rather than naming it.
    ///
    /// Read through <see cref="EntityStore.StatDisplay"/> rather than through
    /// <see cref="EntityStore.StatName"/>, which is the one place in this file that distinction
    /// matters. StatName now spaces out a key the shipped table has no row for, and the 244 tokens
    /// naming an attribute name exactly four between them - Strength, Intellect, Agility and the
    /// stand-in - so spacing the fourth out would fill this field with "Primary Attribute" on 117
    /// tokens and assert an attribute the game does not have.
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
/// The tags are the owning row's. An overflow carries none of its own, and the damage school a token
/// speaks is a fact about the entity rather than about the sentence.
/// </summary>
public sealed record PublicGemOverflow(string Description)
{
    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public string? IconUrl { get; init; }

    public static PublicGemOverflow? From(GemOverflow? overflow, IReadOnlyList<string>? tags, CodexView view) =>
        overflow is null
            ? null
            : new PublicGemOverflow(view.PlainText(overflow.Description, overflow.Placeholders, tags)!)
            {
                Placeholders = view.Spent(overflow.Description, overflow.Placeholders)
                    .Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags))
                    .ToArray() is { Length: > 0 } spent
                    ? spent
                    : null,
                IconUrl = overflow.Icon is null ? null : view.Icon(overflow.Icon),
            };
}
