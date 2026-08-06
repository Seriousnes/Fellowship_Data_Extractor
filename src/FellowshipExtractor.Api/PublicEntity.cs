using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Api;

public sealed record PublicEntity
{
    public string? Type { get; init; }

    public int? NetId { get; init; }

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

    public int? Talent { get; init; }

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

    private static IReadOnlyDictionary<string, string>? Art(Entity entity, CodexView view)
    {
        if (entity.IconByRarity is not { Count: > 0 } art) return null;

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
        string text,
        IReadOnlyDictionary<string, Placeholder>? tokens,
        IReadOnlyList<string>? tags,
        CodexView view) =>
        view.Spent(text, tokens)
            .Select((token, at) => PublicPlaceholder.From(token, view, at + 1, tags))
            .ToArray() is { Length: > 0 } spent
            ? spent
            : null;

    private static IReadOnlyList<PublicStatSlot>? Slots(Entity entity, CodexView view) =>
        entity.Stats is not { Count: > 0 } slots
            ? null
            : [.. slots.Select(slot =>
                new PublicStatSlot(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))];

    internal static IReadOnlyList<string>? Named(IEnumerable<string> values) =>
        values.ToArray() is { Length: > 0 } named ? named : null;
}

public sealed record PublicStatSlot(string Stat, int Weight, string Kind);

public sealed record PublicReference(string? Type, int NetId, string Name)
{
    public static PublicReference? From(IconSource? source, CodexView view) =>
        source is null ? null : new PublicReference(
            PublicTypes.SlugOf(source.Table), source.NetId, view.Name(source.Table, source.NetId));
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
