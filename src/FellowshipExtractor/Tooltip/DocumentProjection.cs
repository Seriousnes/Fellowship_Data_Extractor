using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// One resolved row as its type's document. <see cref="From"/> names every member of
/// <see cref="EntityType"/> and null, with no discard, so a type added to the enum is reported here
/// as CS8509 until it has a document to become.
/// </summary>
public static class DocumentProjection
{
    /// <summary>
    /// The type of the row a table and an id name together, for the pointers a document carries. A
    /// netId is an index within its own table and 3,261 of them are live in more than one, so the
    /// table is half the address and a lookup on the id alone answers about the wrong row.
    /// </summary>
    public delegate EntityType? TypeLookup(string table, int netId);

    // The only value the switch below leaves uncovered is an unnamed one, which is a cast of an
    // integer no member has and which nothing here produces. CS8509, which is what reports a member
    // of the enum going unhandled, stays live.
#pragma warning disable CS8524

    /// <summary>
    /// One resolved row as an instance of its type's document class. Each arm takes the fields its
    /// type states and leaves the rest of <see cref="Entity"/> behind, so a document carries no field
    /// its type never fills.
    /// </summary>
    public static EntityDocument From(Entity entity, TypeLookup lookup) => entity.Type switch
    {
        EntityType.Ability => new Ability
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Detail = entity.Detail,
            Icon = entity.Icon,
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
            ScalesWithHaste = entity.ScalesWithHaste,
            ScalesOff = entity.ScalesOff,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Schools = Schools(entity.Tags),
        },

        EntityType.Item => new Item
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            ArmorType = entity.ArmorType is { } armor ? Enum.Parse<ArmorType>(armor) : null,
            PrimaryStat = entity.PrimaryStat is { } stat ? Enum.Parse<PrimaryStat>(stat) : null,
            Hero = entity.Heroes is { Count: 1 } only ? Enum.Parse<Hero>(only[0]) : null,
            Heroes = entity.Heroes?.Select(Enum.Parse<Hero>).ToArray(),
            Slot = entity.Slot,
            GemSlot = entity.GemSlot,
            UniqueEquipped = entity.UniqueEquipped,

            // A rule set naming a rung is the build fixing the item at it. Parsing through the
            // generated enum is what keeps that rung's name out of this file: Relic, Weapon and the
            // Default and SetBonus rule sets name no rung and parse to none.
            Rarity = Enum.TryParse<Rarity>(entity.RuleSet, out var fixedRarity) ? fixedRarity : null,

            RuleSet = entity.RuleSet,
            SetBonus = entity.SetBonus,
            Stats = entity.Stats,
            Description = entity.Description,
            Icon = entity.Icon,
            IconByRarity = entity.IconByRarity,
            GrantsId = entity.GrantsId,
            LegendaryStatIds = entity.LegendaryStatIds,
        },

        EntityType.GemTier => new GemTier
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Gem = Enum.Parse<GemTypeName>(entity.GemType!),
            Tier = entity.GemTier!.Value,
            Power = entity.GemPower!.Value,

            // The rung is joined on its number, which is what the generated enum's member values
            // are. The word beside it is the one a gem prints and the enum's members are the filed
            // names, and the gem vocabulary renames a rung the filed ladder does not.
            Rarity = (Rarity)entity.RarityTier!.Value,

            Icon = entity.Icon,
        },

        EntityType.TalentSlot => new TalentSlot
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Hero = Enum.Parse<Hero>(entity.Heroes!.Single()),
            Row = entity.TalentRow!.Value,
            Column = entity.TalentColumn!.Value,
            Cost = entity.TalentCost!.Value,

            // The slot-borne talent has no augmentation to point at, so it takes the slot's own id,
            // which is the id `Implied` gives the talent it makes from this row.
            TalentId = entity.Augmentation ?? entity.NetId!.Value,
        },

        EntityType.Effect => new Effect
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Duration = entity.Duration,
            Period = entity.Period,
            Cooldown = entity.Cooldown,
            Range = entity.Range,
            Radius = entity.Radius,
            Cost = entity.Cost,
            TickrateIgnoresHaste = entity.TickrateIgnoresHaste,
            ScalesOff = entity.ScalesOff,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Schools = Schools(entity.Tags),
            NameBorrowedFrom = Reference(entity.NameInheritedFrom, lookup),
            ArtBorrowedFrom = Reference(entity.IconInheritedFrom, lookup),
        },

        EntityType.Creature => new Creature
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Icon = entity.Icon,
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Mount => new Mount
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.Currency => new Currency
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.CompanionPet => new CompanionPet
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.MountPower => new MountPower
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.SetBonus => new Fellowship.SDK.Documents.SetBonus
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Pieces = entity.SetCount,
            Description = entity.Description,
            Icon = entity.Icon,
            PowerId = entity.Augmentation,
            AllowedRoles = entity.AllowedRoles,
            AllowedAttributes = entity.AllowedAttributes?.Select(Enum.Parse<PrimaryStat>).ToArray(),
        },

        EntityType.GemType => new Fellowship.SDK.Documents.GemType
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Gem = Enum.Parse<GemTypeName>(entity.GemType!),
            Attribute = entity.GemAttribute!,
            Icon = entity.GemOverflow?.Icon,
            OverflowDescription = entity.GemOverflow?.Description,
            OverflowPlaceholders = Spent(entity.GemOverflow?.Description, entity.GemOverflow?.Placeholders),
        },

        EntityType.GemTrait => new GemTrait
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Gem = Enum.Parse<GemTypeName>(entity.GemType!),
            Node = entity.GemNode!.Value,
            RequiredPower = entity.RequiredGemPower!.Value,
            ReplacesId = entity.Replaces,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Talent => new Fellowship.SDK.Documents.Talent
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Range = entity.Range,
            Radius = entity.Radius,
            Cost = entity.Cost,
        },

        EntityType.Finesse => new Finesse
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Trait => new Trait
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Category = entity.TraitCategory,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            PowerId = entity.Augmentation,
            Description = entity.Description,
            Icon = entity.Icon,
            RankRuns = entity.RankRuns,
        },

        EntityType.TraitPower => new TraitPower
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.SetBonusPower => new SetBonusPower
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            ScalesOff = entity.ScalesOff,
        },

        EntityType.RelicPower => new RelicPower
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Range = entity.Range,
            ScalesWithHaste = entity.ScalesWithHaste,
            Schools = Schools(entity.Tags),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.WeaponPower => new WeaponPower
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            ChannelTime = entity.ChannelTime,
            ChannelTick = entity.ChannelTick,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Period = entity.Period,
            Range = entity.Range,
            Radius = entity.Radius,
            ScalesWithHaste = entity.ScalesWithHaste,
            Schools = Schools(entity.Tags),
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.LegendaryStat => new LegendaryStat
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            ScalesOff = entity.ScalesOff,
        },

        null => throw new InvalidOperationException($"{entity.Name} has no type and should not have been projected."),
    };
#pragma warning restore CS8524

    /// <summary>
    /// The extra document a row implies. One talent slot carries its talent's text rather than
    /// pointing at an augmentation, so that row is a slot and a talent both, and the talent takes
    /// the slot's own id.
    /// </summary>
    public static EntityDocument? Implied(Entity entity) =>
        entity is { Type: EntityType.TalentSlot, SlotBorneTalent: true }
            ? new Fellowship.SDK.Documents.Talent
            {
                Id = entity.NetId!.Value,
                Name = Name(entity),
                Description = entity.Description,
                Icon = entity.Icon,
            }
            : null;

    /// <summary>
    /// The value <see cref="EntityDocument.Name"/> takes: the shipped display name where it is
    /// written as words, and null where it is a build label.
    /// </summary>
    private static string? Name(Entity entity) =>
        entity.DisplayName is { } name && WrittenAsWords(name) ? name : null;

    /// <summary>
    /// Whether a shipped name is spelled the way a name a player reads is spelled.
    ///
    /// Three characters and one join fail it. A dot or an underscore is a path's punctuation, and,
    /// measured over the documents this projection writes, the 2,503 distinct names that pass carry
    /// neither. A square bracket is how this build marks a row that was never meant to ship. And a
    /// capital straight after a lower case letter or a digit is the join in a run-together compound,
    /// which is the shape of every label the designers left in the field: it catches GearToken and
    /// StarMap inside the names that do carry spaces, so the two shapes are one test rather than a
    /// bracket rule with a compound rule beside it.
    /// </summary>
    private static bool WrittenAsWords(string name)
    {
        for (var at = 0; at < name.Length; at++)
        {
            if (name[at] is '.' or '_' or '[' or ']') return false;

            if (at > 0 && char.IsUpper(name[at]) &&
                (char.IsLower(name[at - 1]) || char.IsDigit(name[at - 1])))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The damage schools a row is, out of its tag list. The schools are the one family of these
    /// tags a player reads off a tooltip; Skill.Secondary and TrackCooldown are the build talking to
    /// itself and reach no reader.
    ///
    /// The rule is `Codex.Schools`, copied. The prefix carries no dot because Magic and Physical are
    /// each a tag in their own right beside Magic.Fire and Physical.Bleed.
    /// </summary>
    private static IReadOnlyList<string>? Schools(IReadOnlyList<string>? tags) =>
        (tags ?? [])
            .Where(tag => tag.StartsWith("Magic", StringComparison.Ordinal)
                       || tag.StartsWith("Physical", StringComparison.Ordinal))
            .Select(Spaced)
            .ToArray() is { Length: > 0 } schools
            ? schools
            : null;

    /// <summary>
    /// A shipped identifier as a reader reads it: dotted segments spaced out and runs of capitals
    /// broken up, so Magic.Fire prints as Magic / Fire and DamageScale as Damage Scale.
    /// `Codex.Spaced`, copied, because the extractor cannot reference the API.
    /// </summary>
    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }

    /// <summary>
    /// A row's tokens as a list, ordered by where the text it is spent in first spends each one, then
    /// by key ordinal, each carrying its key. A token the text never spends sorts after every token it
    /// does.
    ///
    /// Measured over the documents this projection writes, 212 placeholder lists reach this method:
    /// 206 ordered against a description-and-detail text, which spend 363 `{Token}` occurrences
    /// between them, and 6 against a gem's overflow sentence, which spend 24. 8 tokens are spent by
    /// no text and sort last. `CodexView.Spent` and `CodexView.Spends(string, string)`, copied,
    /// because the extractor cannot reference the API.
    /// </summary>
    private static IReadOnlyList<Placeholder>? Spent(string? text, IReadOnlyDictionary<string, Placeholder>? tokens) =>
        tokens is not { Count: > 0 }
            ? null
            : [.. tokens
                .OrderBy(spent => Spends(text ?? string.Empty, spent.Key))
                .ThenBy(spent => spent.Key, StringComparer.Ordinal)
                .Select(spent => spent.Value with { Key = spent.Key })];

    /// <summary>
    /// Everything of a row a substitution can be spent in, as one string. A detail line spends tokens
    /// as readily as the description does, and a token spent only there sorts by where it falls
    /// rather than after everything. `CodexView.Spends(Entity)`, copied.
    /// </summary>
    private static string Spends(Entity entity) =>
        string.Join('\n', new[] { entity.Description }.Concat(entity.Detail ?? []).OfType<string>());

    /// <summary>
    /// Where a description first spends a token, or past the end of it where the text never does.
    /// `CodexView.Spends(string, string)`, copied.
    /// </summary>
    private static int Spends(string text, string name)
    {
        var at = text.IndexOf($"{{{name}}}", StringComparison.Ordinal);

        return at < 0 ? int.MaxValue : at;
    }

    /// <summary>Another row, addressed by its type rather than by the table it is filed in.</summary>
    private static EntityReference? Reference(IconSource? source, TypeLookup lookup) =>
        source is null || lookup(source.Table, source.NetId) is not { } type
            ? null
            : new EntityReference(type, source.NetId, null);
}
