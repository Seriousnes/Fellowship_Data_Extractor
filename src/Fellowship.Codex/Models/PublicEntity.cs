using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record PublicEntity
{
    public required string Type { get; init; }

    public required int Id { get; init; }

    public required string Name { get; init; }

    public string? Slot { get; init; }
    public bool? GemSlot { get; init; }
    public string? ArmorType { get; init; }
    public string? PrimaryStat { get; init; }
    public IReadOnlyList<string>? Heroes { get; init; }
    public IReadOnlyList<PublicReference>? SetBonus { get; init; }
    public int? SetCount { get; init; }
    public bool? UniqueEquipped { get; init; }

    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<PublicStatSlot>? Stats { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string>? Detail { get; init; }

    public string? Icon { get; init; }

    public string? Background { get; init; }

    public IReadOnlyDictionary<string, string>? IconByRarity { get; init; }

    public PublicReference? ArtBorrowedFrom { get; init; }

    public PublicReference? NameBorrowedFrom { get; init; }

    public PublicReference? Owner { get; init; }

    public PublicReference? AppliedBy { get; init; }

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

    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<AbilityCoefficient>? Coefficients { get; init; }

    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public int? TalentRow { get; init; }
    public int? TalentColumn { get; init; }
    public int? TalentCost { get; init; }

    public PublicGemOverflow? GemOverflow { get; init; }
    public string? GemType { get; init; }
    public int? GemPower { get; init; }
    public string? Rarity { get; init; }
    public int? GemNode { get; init; }
    public int? RequiredGemPower { get; init; }

    public PublicReference? Replaces { get; init; }

    public string? TraitCategory { get; init; }
    public int? MaxRank { get; init; }
    public int? RandomChance { get; init; }
    public IReadOnlyList<PublicRankRun>? RankRuns { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<string>? AllowedAttributes { get; init; }

    public PublicReference? Grants { get; init; }

    public IReadOnlyList<PublicReference>? LegendaryStats { get; init; }

    public PublicReference? Power { get; init; }

    public PublicReference? Talent { get; init; }

    public string? Rank { get; init; }
    public double? Health { get; init; }
    public double? Armor { get; init; }
    public double? Resist { get; init; }
    public int? KillScore { get; init; }
    public int? SpiritPoints { get; init; }
    public bool? CountsTowardScore { get; init; }
    public double? Intellect { get; init; }
    public double? Strength { get; init; }
    public double? Agility { get; init; }
    public string? Scaling { get; init; }

    public IReadOnlyList<PublicCreatureAbility>? Abilities { get; init; }

    public IReadOnlyList<PublicCreatureAbility>? Passives { get; init; }

    public IReadOnlyList<PublicReference>? FoundIn { get; init; }

    public int? MaxPlayers { get; init; }
    public double? QualifierTime { get; init; }
    public int? RequiredScore { get; init; }
    public int? MinimumHeroRank { get; init; }

    public IReadOnlyList<PublicReference>? Creatures { get; init; }

    public static PublicEntity From(EntityDocument document, ICodexViewService view, string origin)
    {
        var tokens = CodexView.PlaceholdersOf(document);
        var tags = FellowshipCodex.Schools(document);

        return new PublicEntity
        {
            Type = EntityTypes.Slug(EntityTypes.TypeOf(document)),
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            Slot = (document as ItemDocument)?.Slot,
            GemSlot = (document as ItemDocument)?.GemSlot,
            ArmorType = (document as ItemDocument)?.ArmorType?.ToString(),
            PrimaryStat = (document as ItemDocument)?.PrimaryStat?.ToString(),
            Heroes = HeroesOf(document),
            SetBonus = References((document as ItemDocument)?.SetBonus, view, origin),
            SetCount = (document as SetBonusDocument)?.Pieces,
            UniqueEquipped = (document as ItemDocument)?.UniqueEquipped,

            Schools = Named(tags),

            Stats = Slots(document, view),
            Description = view.PlainText(DocumentFields.Text(document, nameof(AbilityDocument.Description)), tokens, tags),
            Detail = DocumentFields.Words(document, nameof(AbilityDocument.Detail)) is { Count: > 0 } lines
                ? [.. lines.Select(line => view.PlainText(line, tokens, tags)!)]
                : null,
            Icon = DocumentFields.Text(document, nameof(ItemDocument.Icon)) is { } icon ? view.Icon(icon) : null,
            Background = (document as DungeonDocument)?.Background is { } behind ? view.Icon(behind) : null,
            IconByRarity = Art(document, view),
            ArtBorrowedFrom = PublicReference.From((document as EffectDocument)?.ArtBorrowedFrom, view, origin),
            NameBorrowedFrom = PublicReference.From((document as EffectDocument)?.NameBorrowedFrom, view, origin),
            Owner = PublicReference.From(OwnerOf(document), view, origin),
            AppliedBy = PublicReference.From((document as EffectDocument)?.AppliedBy, view, origin),
            CastTime = DocumentFields.Number(document, nameof(AbilityDocument.CastTime)),
            ChannelTime = DocumentFields.Number(document, nameof(AbilityDocument.ChannelTime)),
            ChannelTick = DocumentFields.Number(document, nameof(AbilityDocument.ChannelTick)),
            Cooldown = DocumentFields.Number(document, nameof(AbilityDocument.Cooldown)),
            Charges = DocumentFields.Number(document, nameof(AbilityDocument.Charges)),
            Cost = DocumentFields.Number(document, nameof(AbilityDocument.Cost)),
            Resource = DocumentFields.Text(document, nameof(AbilityDocument.Resource)),
            Range = DocumentFields.Number(document, nameof(AbilityDocument.Range)),
            Radius = DocumentFields.Number(document, nameof(AbilityDocument.Radius)),
            Duration = DocumentFields.Number(document, nameof(AbilityDocument.Duration)),
            Period = DocumentFields.Number(document, nameof(AbilityDocument.Period)),
            ScalesOff = Named(view.Codex.Scaling(DocumentFields.Words(document, nameof(AbilityDocument.ScalesOff)))),
            Coefficients = (document as AbilityDocument)?.Coefficients,
            Placeholders = Tokens(tokens, tags, view),
            TalentRow = (document as TalentSlotDocument)?.Row,
            TalentColumn = (document as TalentSlotDocument)?.Column,
            TalentCost = (document as TalentSlotDocument)?.Cost,
            GemOverflow = document is GemTypeDocument gem ? PublicGemOverflow.From(gem, view) : null,
            GemType = GemTypeName(document),
            GemPower = (document as GemDocument)?.Power,
            Rarity = view.Rarity(document),
            GemNode = (document as GemTraitDocument)?.Node,
            RequiredGemPower = (document as GemTraitDocument)?.RequiredPower,
            Replaces = PublicReference.From((document as GemTraitDocument)?.Replaces, view, origin),
            TraitCategory = (document as TraitDocument)?.Category,
            MaxRank = (document as TraitDocument)?.MaxRank,
            RandomChance = (document as TraitDocument)?.RandomChance,
            RankRuns = document is TraitDocument { RankRuns: { } runs } ? [.. runs.Select(PublicRankRun.From)] : null,
            AllowedRoles = (document as SetBonusDocument)?.AllowedRoles,
            AllowedAttributes = document is SetBonusDocument { AllowedAttributes: { } attributes }
                ? [.. attributes.Select(attribute => attribute.ToString())]
                : null,
            Grants = PublicReference.From((document as ItemDocument)?.Grants, view, origin),
            LegendaryStats = References((document as ItemDocument)?.LegendaryStats, view, origin),
            Power = PublicReference.From(PowerOf(document), view, origin),
            Talent = PublicReference.From((document as TalentSlotDocument)?.Talent, view, origin),

            Rank = (document as CreatureDocument)?.Rank?.ToString(),
            Health = (document as CreatureDocument)?.Health,
            Armor = (document as CreatureDocument)?.Armor,
            Resist = (document as CreatureDocument)?.Resist,
            KillScore = (document as CreatureDocument)?.KillScore,
            SpiritPoints = (document as CreatureDocument)?.SpiritPoints,
            CountsTowardScore = (document as CreatureDocument)?.CountsTowardScore,
            Intellect = (document as CreatureDocument)?.Intellect,
            Strength = (document as CreatureDocument)?.Strength,
            Agility = (document as CreatureDocument)?.Agility,
            Scaling = (document as CreatureDocument)?.Scaling,
            Abilities = Wielded((document as CreatureDocument)?.Abilities, view, origin),
            Passives = Wielded((document as CreatureDocument)?.Passives, view, origin),
            FoundIn = References((document as CreatureDocument)?.FoundIn, view, origin),

            MaxPlayers = (document as DungeonDocument)?.MaxPlayers,
            QualifierTime = (document as DungeonDocument)?.QualifierTime,
            RequiredScore = (document as DungeonDocument)?.RequiredScore,
            MinimumHeroRank = (document as DungeonDocument)?.MinimumHeroRank,
            Creatures = document is DungeonDocument dungeon ? Found(dungeon, view, origin) : null,
        };
    }

    private static EntityReference? OwnerOf(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.Owner,
        EffectDocument effect => effect.Owner,
        _ => null,
    };

    private static EntityReference? PowerOf(EntityDocument document) => document switch
    {
        TraitDocument trait => trait.Power,
        SetBonusDocument bonus => bonus.Power,
        _ => null,
    };

    private static IReadOnlyList<PublicReference>? References(
        IReadOnlyList<EntityReference>? sources, ICodexViewService view, string origin) =>
        sources?.Select(source => PublicReference.From(source, view, origin)).OfType<PublicReference>().ToArray()
            is { Length: > 0 } named
            ? named
            : null;

    private static IReadOnlyList<PublicCreatureAbility>? Wielded(
        IReadOnlyList<CreatureAbility>? held, ICodexViewService view, string origin) =>
        held?.Select(slot => new PublicCreatureAbility(
                PublicReference.From(slot.Ability, view, origin)!, slot.Phase, slot.Shown))
            .ToArray() is { Length: > 0 } wielded
            ? wielded
            : null;

    private static IReadOnlyList<PublicReference>? Found(
        DungeonDocument dungeon, ICodexViewService view, string origin) =>
        view.Store.Entities
            .OfType<CreatureDocument>()
            .Where(creature => creature.FoundIn?.Any(level => level.Id == dungeon.Id) is true)
            .Select(creature => PublicReference.Of(EntityType.Creature, creature.Id, view, origin))
            .ToArray() is { Length: > 0 } dwelling
            ? dwelling
            : null;

    private static IReadOnlyList<string>? HeroesOf(EntityDocument document) => document switch
    {
        ItemDocument { Heroes: { Count: > 0 } list } => [.. list.Select(hero => hero.ToString())],
        TalentSlotDocument slot => [slot.Hero.ToString()],
        _ => null,
    };

    private static string? GemTypeName(EntityDocument document) => document switch
    {
        GemDocument gem => gem.Type.ToString(),
        GemTraitDocument trait => trait.Gem.ToString(),
        GemTypeDocument type => type.Type.ToString(),
        _ => null,
    };

    private static IReadOnlyDictionary<string, string>? Art(EntityDocument document, ICodexViewService view)
    {
        if (document is not ItemDocument { IconByRarity: { Count: > 0 } art }) return null;

        var printed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (rung, icon) in art)
        {
            var known = view.Store.Settings.Rarities.FirstOrDefault(r =>
                string.Equals(r.Name, rung, StringComparison.OrdinalIgnoreCase));

            if (known is not null) printed[known.DisplayName] = view.Icon(icon);
        }

        return printed.Count == 0 ? null : printed;
    }

    private static IReadOnlyList<PublicPlaceholder>? Tokens(
        IReadOnlyList<Placeholder> tokens, IReadOnlyList<string> tags, ICodexViewService view) =>
        tokens.Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags)).ToArray() is { Length: > 0 } spent
            ? spent
            : null;

    private static IReadOnlyList<PublicStatSlot>? Slots(EntityDocument document, ICodexViewService view) =>
        document is ItemDocument { Stats: { Count: > 0 } slots }
            ? [.. slots.Select(slot => new PublicStatSlot(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))]
            : null;

    internal static IReadOnlyList<string>? Named(IEnumerable<string> values) =>
        values.ToArray() is { Length: > 0 } named ? named : null;
}

public sealed record PublicStatSlot(string Stat, int Weight, string Kind);

public sealed record PublicReference(string Type, int Id, string Name, string Resource)
{
    public static PublicReference? From(EntityReference? source, ICodexViewService view, string origin) =>
        source is null ? null : Of(source.Type, source.Id, view, origin);

    public static PublicReference Of(EntityType type, int id, ICodexViewService view, string origin)
    {
        var slug = EntityTypes.Slug(type);

        return new PublicReference(
            slug,
            id,
            view.Name(type, id),
            $"{origin}/api/{slug}/{id.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }
}

public sealed record PublicCreatureAbility(PublicReference Ability, string Phase, bool Shown);

public sealed record PublicPlaceholder
{
    public int Ordinal { get; init; }

    public required string Reading { get; init; }

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
        Placeholder token, ICodexViewService view, int ordinal, IReadOnlyList<string>? tags) => new()
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

public sealed record PublicGemOverflow(string Description)
{
    public IReadOnlyList<PublicPlaceholder>? Placeholders { get; init; }

    public string? Icon { get; init; }

    public static PublicGemOverflow? From(GemTypeDocument gem, ICodexViewService view)
    {
        if (gem.OverflowDescription is not { } description) return null;

        var tokens = gem.OverflowPlaceholders ?? [];

        return new PublicGemOverflow(view.PlainText(description, tokens, [])!)
        {
            Placeholders = tokens.Select((token, at) => PublicPlaceholder.From(token, view, at + 1, [])).ToArray()
                is { Length: > 0 } spent
                ? spent
                : null,
            Icon = gem.Icon is null ? null : view.Icon(gem.Icon),
        };
    }
}
