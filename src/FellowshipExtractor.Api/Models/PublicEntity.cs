using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Api.Models;

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
    public IReadOnlyList<string>? SetBonus { get; init; }
    public int? SetCount { get; init; }
    public bool? UniqueEquipped { get; init; }

    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<PublicStatSlot>? Stats { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string>? Detail { get; init; }

    public string? IconUrl { get; init; }

    public IReadOnlyDictionary<string, string>? IconUrlByRarity { get; init; }

    public PublicReference? ArtBorrowedFrom { get; init; }

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

    public IReadOnlyList<string>? ScalesOff { get; init; }

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

    public int? Replaces { get; init; }

    public string? TraitCategory { get; init; }
    public int? MaxRank { get; init; }
    public int? RandomChance { get; init; }
    public IReadOnlyList<PublicRankRun>? RankRuns { get; init; }
    public IReadOnlyList<string>? AllowedRoles { get; init; }
    public IReadOnlyList<string>? AllowedAttributes { get; init; }

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
            Slot = (document as ItemDocument)?.Slot,
            GemSlot = (document as ItemDocument)?.GemSlot,
            ArmorType = (document as ItemDocument)?.ArmorType?.ToString(),
            PrimaryStat = (document as ItemDocument)?.PrimaryStat?.ToString(),
            Heroes = HeroesOf(document),
            SetBonus = (document as ItemDocument)?.SetBonus,
            SetCount = (document as SetBonusDocument)?.Pieces,
            UniqueEquipped = (document as ItemDocument)?.UniqueEquipped,

            Schools = Named(tags),

            Stats = Slots(document, view),
            Description = view.PlainText(DocumentFields.Text(document, nameof(AbilityDocument.Description)), tokens, tags),
            Detail = DocumentFields.Words(document, nameof(AbilityDocument.Detail)) is { Count: > 0 } lines
                ? [.. lines.Select(line => view.PlainText(line, tokens, tags)!)]
                : null,
            IconUrl = DocumentFields.Text(document, nameof(ItemDocument.Icon)) is { } icon ? view.Icon(icon) : null,
            IconUrlByRarity = Art(document, view),
            ArtBorrowedFrom = PublicReference.From((document as EffectDocument)?.ArtBorrowedFrom, view),
            NameBorrowedFrom = PublicReference.From((document as EffectDocument)?.NameBorrowedFrom, view),
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
            Placeholders = Tokens(tokens, tags, view),
            TalentRow = (document as TalentSlotDocument)?.Row,
            TalentColumn = (document as TalentSlotDocument)?.Column,
            TalentCost = (document as TalentSlotDocument)?.Cost,
            GemOverflow = document is GemTypeDocument gem ? PublicGemOverflow.From(gem, view) : null,
            GemType = GemTypeName(document),
            GemTier = (document as GemDocument)?.Tier,
            GemPower = (document as GemDocument)?.Power,
            Rarity = view.Rarity(document),
            GemNode = (document as GemTraitDocument)?.Node,
            RequiredGemPower = (document as GemTraitDocument)?.RequiredPower,
            Replaces = (document as GemTraitDocument)?.ReplacesId,
            TraitCategory = (document as TraitDocument)?.Category,
            MaxRank = (document as TraitDocument)?.MaxRank,
            RandomChance = (document as TraitDocument)?.RandomChance,
            RankRuns = document is TraitDocument { RankRuns: { } runs } ? [.. runs.Select(PublicRankRun.From)] : null,
            AllowedRoles = (document as SetBonusDocument)?.AllowedRoles,
            AllowedAttributes = document is SetBonusDocument { AllowedAttributes: { } attributes }
                ? [.. attributes.Select(attribute => attribute.ToString())]
                : null,
            Grants = Granted(document, view),
            LegendaryStats = document is ItemDocument { LegendaryStatIds: { Count: > 0 } ids }
                ? [.. ids.SelectMany(id => view.Store.All(EntityType.LegendaryStat, id)).Select(stat => From(stat, view))]
                : null,
        };
    }

    private static IReadOnlyList<string>? HeroesOf(EntityDocument document) => document switch
    {
        ItemDocument { Heroes: { Count: > 0 } list } => [.. list.Select(hero => hero.ToString())],
        TalentSlotDocument slot => [slot.Hero.ToString()],
        _ => null,
    };

    private static PublicEntity? Granted(EntityDocument document, CodexView view)
    {
        (EntityType Type, int Id)? address = document switch
        {
            ItemDocument { GrantsId: { } id } => Power(view.Store, id),
            TraitDocument { PowerId: { } id } => (EntityType.TraitPower, id),
            SetBonusDocument { PowerId: { } id } => (EntityType.SetBonusPower, id),
            TalentSlotDocument slot => (EntityType.Talent, slot.TalentId),
            _ => null,
        };

        if (address is not { } found) return null;

        return view.Store.All(found.Type, found.Id) is { Count: > 0 } readings ? From(readings[0], view) : null;
    }

    private static (EntityType Type, int Id)? Power(EntityStore store, int id) =>
        store.All(EntityType.RelicPower, id).Count > 0 ? (EntityType.RelicPower, id)
        : store.All(EntityType.WeaponPower, id).Count > 0 ? (EntityType.WeaponPower, id)
        : null;

    private static string? GemTypeName(EntityDocument document) => document switch
    {
        GemDocument tier => tier.Type.ToString(),
        GemTraitDocument trait => trait.Gem.ToString(),
        GemTypeDocument type => type.Type.ToString(),
        _ => null,
    };

    private static IReadOnlyDictionary<string, string>? Art(EntityDocument document, CodexView view)
    {
        if (document is not ItemDocument { IconByRarity: { Count: > 0 } art }) return null;

        var printed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (rung, icon) in art)
        {
            var known = view.Store.Vocabulary.Rarities.FirstOrDefault(r =>
                string.Equals(r.Name, rung, StringComparison.OrdinalIgnoreCase));

            if (known is not null) printed[known.DisplayName] = view.Icon(icon);
        }

        return printed.Count == 0 ? null : printed;
    }

    private static IReadOnlyList<PublicPlaceholder>? Tokens(
        IReadOnlyList<Placeholder> tokens, IReadOnlyList<string> tags, CodexView view) =>
        tokens.Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags)).ToArray() is { Length: > 0 } spent
            ? spent
            : null;

    private static IReadOnlyList<PublicStatSlot>? Slots(EntityDocument document, CodexView view) =>
        document is ItemDocument { Stats: { Count: > 0 } slots }
            ? [.. slots.Select(slot => new PublicStatSlot(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))]
            : null;

    internal static IReadOnlyList<string>? Named(IEnumerable<string> values) =>
        values.ToArray() is { Length: > 0 } named ? named : null;
}

public sealed record PublicStatSlot(string Stat, int Weight, string Kind);

public sealed record PublicReference(string Type, int Id, string Name)
{
    public static PublicReference? From(EntityReference? source, CodexView view) =>
        source is null ? null : new PublicReference(
            EntityTypes.Slug(source.Type), source.Id, view.Name(source.Type, source.Id));
}

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

    public string? IconUrl { get; init; }

    public static PublicGemOverflow? From(GemTypeDocument gem, CodexView view)
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
