using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tooltip;

public static class DocumentProjection
{
    public delegate EntityType? TypeLookup(string table, int netId);

#pragma warning disable CS8524

    public static EntityDocument From(Entity entity, TypeLookup lookup) => entity.Type switch
    {
        EntityType.Ability => new AbilityDocument
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

        EntityType.Item => new ItemDocument
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

            Rarity = Enum.TryParse<Rarity>(entity.RuleSet, out var fixedRarity) ? fixedRarity : null,

            Rarities = entity.Rarities?.Select(Enum.Parse<Rarity>).Order().ToArray(),

            RuleSet = entity.RuleSet,
            SetBonus = entity.SetBonus,
            Stats = entity.Stats,
            Description = entity.Description,
            Icon = entity.Icon,
            IconByRarity = entity.IconByRarity,
            GrantsId = entity.GrantsId,
            LegendaryStatIds = entity.LegendaryStatIds,
        },

        EntityType.GemTier => new GemDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Type = Enum.Parse<GemType>(entity.GemType!),
            Tier = entity.GemTier!.Value,
            Power = entity.GemPower!.Value,

            Rarity = (Rarity)entity.RarityTier!.Value,

            Icon = entity.Icon,
        },

        EntityType.TalentSlot => new TalentSlotDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Hero = Enum.Parse<Hero>(entity.Heroes!.Single()),
            Row = entity.TalentRow!.Value,
            Column = entity.TalentColumn!.Value,
            Cost = entity.TalentCost!.Value,

            TalentId = entity.Augmentation ?? entity.NetId!.Value,
        },

        EntityType.Effect => new EffectDocument
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

        EntityType.Creature => new CreatureDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Icon = entity.Icon,
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Mount => new MountDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.Currency => new CurrencyDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Rarity = entity.RarityTier is { } currencyTier ? (Rarity)currencyTier : null,
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.CompanionPet => new CompanionPetDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.MountPower => new MountPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.SetBonus => new SetBonusDocument
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

        EntityType.GemType => new GemTypeDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Type = Enum.Parse<GemType>(entity.GemType!),
            Attribute = entity.GemAttribute!,
            Icon = entity.GemOverflow?.Icon,
            OverflowDescription = entity.GemOverflow?.Description,
            OverflowPlaceholders = Spent(entity.GemOverflow?.Description, entity.GemOverflow?.Placeholders),
        },

        EntityType.GemTrait => new GemTraitDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Gem = Enum.Parse<GemType>(entity.GemType!),
            Node = entity.GemNode!.Value,
            RequiredPower = entity.RequiredGemPower!.Value,
            ReplacesId = entity.Replaces,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Talent => new TalentDocument
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

        EntityType.Blessing => new BlessingDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Trait => new TraitDocument
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

        EntityType.TraitPower => new TraitPowerDocument
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

        EntityType.SetBonusPower => new SetBonusPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            ScalesOff = entity.ScalesOff,
        },

        EntityType.RelicPower => new RelicPowerDocument
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

        EntityType.WeaponPower => new WeaponPowerDocument
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

        EntityType.LegendaryStat => new LegendaryStatDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            ScalesOff = entity.ScalesOff,
        },

        null => throw new InvalidOperationException($"{entity.Name} has no type and should not have been projected."),
    };
#pragma warning restore CS8524

    public static EntityDocument? Implied(Entity entity) =>
        entity is { Type: EntityType.TalentSlot, SlotBorneTalent: true }
            ? new TalentDocument
            {
                Id = entity.NetId!.Value,
                Name = Name(entity),
                Description = entity.Description,
                Icon = entity.Icon,
            }
            : null;

    private static string? Name(Entity entity) =>
        entity.DisplayName is { } name && WrittenAsWords(name) ? name : null;

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

    private static IReadOnlyList<string>? Schools(IReadOnlyList<string>? tags) =>
        (tags ?? [])
            .Where(tag => tag.StartsWith("Magic", StringComparison.Ordinal)
                       || tag.StartsWith("Physical", StringComparison.Ordinal))
            .Select(Spaced)
            .ToArray() is { Length: > 0 } schools
            ? schools
            : null;

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

    private static IReadOnlyList<Placeholder>? Spent(string? text, IReadOnlyDictionary<string, Placeholder>? tokens) =>
        tokens is not { Count: > 0 }
            ? null
            : [.. tokens
                .OrderBy(spent => Spends(text ?? string.Empty, spent.Key))
                .ThenBy(spent => spent.Key, StringComparer.Ordinal)
                .Select(spent => spent.Value with { Key = spent.Key })];

    private static string Spends(Entity entity) =>
        string.Join('\n', new[] { entity.Description }.Concat(entity.Detail ?? []).OfType<string>());

    private static int Spends(string text, string name)
    {
        var at = text.IndexOf($"{{{name}}}", StringComparison.Ordinal);

        return at < 0 ? int.MaxValue : at;
    }

    private static EntityReference? Reference(IconSource? source, TypeLookup lookup) =>
        source is null || lookup(source.Table, source.NetId) is not { } type
            ? null
            : new EntityReference(type, source.NetId, null);
}
