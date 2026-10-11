using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class DocumentProjection
{
    public delegate EntityType? TypeLookup(string table, int netId);

    public delegate string? NameLookup(EntityType type, int id);

    public sealed class References(TypeLookup types, NameLookup names)
    {
        public EntityReference? At(IconSource? source) =>
            source is null || types(source.Table, source.NetId) is not { } type
                ? null
                : Of(type, source.NetId);

        public EntityReference? Of(EntityType type, int? id) =>
            id is { } found ? new EntityReference(type, found, names(type, found)) : null;
    }

#pragma warning disable CS8524

    public static EntityDocument From(Entity entity, References references, EffectRoles roles) => entity.Type switch
    {
        EntityType.Ability => new AbilityDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Detail = entity.Detail,
            Icon = entity.Icon,
            Category = entity.Category is { } category ? Enum.Parse<AbilityCategory>(category) : null,
            CastTime = entity.CastTime,
            ChannelTime = entity.ChannelTime,
            ChannelTick = entity.ChannelTick,
            Cooldown = entity.Cooldown,
            Charges = entity.Charges,
            ChargesByLevelType = entity.ChargesByLevelType,
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            Cost = entity.Cost,
            Resource = entity.Resource,
            Generates = Generated(entity),
            Range = entity.Range,
            MinRange = entity.MinRange,
            Radius = entity.Radius,
            Duration = entity.Duration,
            Period = entity.Period,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            ScalesOff = entity.ScalesOff,
            Coefficients = entity.Coefficients,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Schools = Schools(entity.Tags),
            Heroes = Owning(entity),
            Owner = references.At(entity.Owner),
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

            Rarity = Enum.TryParse<Rarity>(entity.RuleSet, out var fixedRarity)
                ? fixedRarity
                : entity.RarityTier is { } statedTier ? (Rarity)statedTier : null,

            Rarities = entity.Rarities?.Select(Enum.Parse<Rarity>).Order().ToArray(),

            RuleSet = entity.RuleSet,
            SetBonus = Referenced(entity.SetBonus, references),
            Stats = entity.Stats,
            Description = entity.Description,
            Acquire = entity.Acquire,
            TitleText = entity.TitleText,
            Icon = entity.Icon,
            IconByRarity = entity.IconByRarity,
            Grants = references.At(Augmentation(entity.GrantsId)),
            LegendaryStats = Referenced(
                entity.LegendaryStatIds?.Select(id => Augmentation(id)).OfType<IconSource>().ToArray(), references),
            Sources = Sourced(entity.Sources, references),
            ItemLevel = entity.ItemLevel,
        },

        EntityType.Gem => new GemDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Type = Enum.Parse<GemType>(entity.GemType!),
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

            Talent = references.Of(EntityType.Talent, entity.Augmentation ?? entity.NetId!.Value)!,
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
            MaxStacks = entity.MaxStacks,
            Infinite = entity.Infinite,
            ScalesOff = entity.ScalesOff,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Schools = Schools(entity.Tags) ?? Schools(entity.ApplierTags),
            Modifiers = entity.Modifiers,
            Generates = Generated(entity),
            Talents = entity.Talents,
            NameBorrowedFrom = references.At(entity.NameInheritedFrom),
            TextureBorrowedFrom = references.At(entity.IconInheritedFrom),
            Heroes = Owning(entity),
            Owner = references.At(entity.Owner),
            AppliedBy = references.At(entity.AppliedBy),
            PartOf = references.Of(EntityType.Ability, roles.PartOf(entity.Name)),
            Role = roles.Role(entity.Name),
        },

        EntityType.Creature => new CreatureDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Icon = entity.Icon,
            Rank = entity.Rank,
            Health = entity.Health,
            Armor = entity.Armor,
            Resist = entity.Resist,
            KillScore = entity.KillScore,
            SpiritPoints = entity.SpiritPoints,
            CountsTowardScore = entity.CountsTowardScore,
            Intellect = entity.Intellect,
            Strength = entity.Strength,
            Agility = entity.Agility,
            Scaling = entity.Scaling,
            Abilities = Abilities(entity.Abilities, references),
            Passives = Abilities(entity.Passives, references),
            FoundIn = Referenced(entity.FoundIn, references),
        },

        EntityType.Dungeon => new DungeonDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Background = entity.Background,
            MaxPlayers = entity.MaxPlayers,
            QualifierTime = entity.QualifierTime,
            RequiredScore = entity.RequiredScore,
            MinimumHeroRank = entity.MinimumHeroRank,
        },

        EntityType.Modifier => new ModifierDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Kind = entity.ModifierKind,
            Generates = Generated(entity),
            MinimumHeroRank = entity.MinimumHeroRank,
        },

        EntityType.Hero => new HeroDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Title = entity.HeroTitle,
            MainStat = entity.PrimaryStat is { } mainStat ? Enum.Parse<PrimaryStat>(mainStat) : null,
            Color = entity.HeroColor,
            Biography = entity.Biography,
            Playstyle = entity.Playstyle,
            SpiritAbility = entity.SpiritAbility,
            Portrait = entity.Icon,
        },

        EntityType.Mount => new MountDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Lore = entity.Lore,
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
            Acquire = entity.Acquire,
            Icon = entity.Icon,
        },

        EntityType.MountPower => new MountPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Generates = Generated(entity),
        },

        EntityType.SetBonus => new SetBonusDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Pieces = entity.SetCount,
            Description = entity.Description,
            Icon = entity.Icon,
            Power = references.At(Augmentation(entity.Augmentation)),
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
            Replaces = references.Of(EntityType.GemTrait, entity.Replaces),
            Grants = Referenced(entity.Granted, references),
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
            Generates = Generated(entity),
        },

        EntityType.Blessing => new BlessingDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
            MaxRank = entity.AbilityMaxRank,
            Slots = entity.PoolSlots,
            AbilityRanks = entity.AbilityRanks,
            Generates = Generated(entity),
        },

        EntityType.Trait => new TraitDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Category = entity.TraitCategory,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Power = references.At(Augmentation(entity.Augmentation)),
            Description = entity.Description,
            Icon = entity.Icon,
            RankRuns = entity.RankRuns,
            Generates = Generated(entity),
        },

        EntityType.TraitPower => new TraitPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
            Generates = Generated(entity),
        },

        EntityType.SetBonusPower => new SetBonusPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            ScalesOff = entity.ScalesOff,
            Generates = Generated(entity),
        },

        EntityType.RelicPower => new RelicPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            Cooldown = entity.Cooldown,
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            Duration = entity.Duration,
            Range = entity.Range,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            Schools = Schools(entity.Tags),
            ScalesOff = entity.ScalesOff,
            Generates = Generated(entity),
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
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            Duration = entity.Duration,
            Period = entity.Period,
            Range = entity.Range,
            Radius = entity.Radius,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            Schools = Schools(entity.Tags),
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
            Generates = Generated(entity),
        },

        EntityType.LegendaryPower => new LegendaryPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            ScalesOff = entity.ScalesOff,
            Generates = Generated(entity),
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

    public static string? Name(Entity entity) =>
        entity.DisplayName is { } name && ReadsAsWords(name) ? name : null;

    private static IconSource? Augmentation(int? netId) =>
        netId is { } id ? new IconSource(TalentTrees.AugmentationTable, id, string.Empty) : null;

    private static IReadOnlyList<EntityReference>? Augmentations(IReadOnlyList<int>? ids, References references) =>
        Referenced(ids?.Select(id => Augmentation(id)).OfType<IconSource>().ToArray(), references);

    private static IReadOnlyList<EntityReference>? Referenced(
        IReadOnlyList<IconSource>? sources, References references) =>
        sources?.Select(references.At).OfType<EntityReference>().ToArray() is { Length: > 0 } named ? named : null;

    private static IReadOnlyList<CreatureAbility>? Abilities(
        IReadOnlyList<PhaseAbility>? held, References references) =>
        held?.Select(slot => references.At(slot.Ability) is { } ability
                ? new CreatureAbility(ability, slot.Phase, slot.Shown)
                : null)
            .OfType<CreatureAbility>()
            .ToArray() is { Length: > 0 } wielded
            ? wielded
            : null;

    private static IReadOnlyList<ItemSource>? Sourced(IReadOnlyList<ItemSource>? sources, References references) =>
        sources?.Select(source => source.Dungeon is { } dungeon
                ? source with { Dungeon = references.Of(dungeon.Type, dungeon.Id) }
                : source)
            .ToArray() is { Length: > 0 } carried
            ? carried
            : null;

    private static bool ReadsAsWords(string name)
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

    private static IReadOnlyList<Hero>? Owning(Entity entity)
    {
        if (entity.OwningHeroes is not { Count: > 0 } claimed) return null;

        return [.. claimed.Select(Enum.Parse<Hero>).Distinct().Order()];
    }

    private static IReadOnlyList<ResourceGain>? Generated(Entity entity) =>
        entity.Generates is { Count: > 0 } gains ? gains : null;
}
