using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class DocumentProjection
{
    public delegate EntityType? TypeLookup(string table, int netId);

    public delegate string? NameLookup(EntityType type, int id);

    public sealed class References(
        TypeLookup types,
        NameLookup names,
        IReadOnlyDictionary<(EntityType Type, int Id), int>? redirects = null,
        IReadOnlyDictionary<(EntityType Type, int Id, string Stat), string>? namesByStat = null,
        string? stat = null,
        ChangeIndex? changes = null,
        AppliedLinks? links = null,
        IReadOnlyDictionary<(EntityType Type, int Id), IReadOnlyList<(EntityType Type, int Id)>>? origins = null)
    {
        public ChangeIndex Changes { get; } = changes ?? ChangeIndex.None;

        public AppliedLinks Links { get; } = links ?? AppliedLinks.None;

        public References For(string? primaryStat) =>
            new(types, names, redirects, namesByStat, primaryStat, Changes, Links, origins);

        public References With(ChangeIndex index) => new(types, names, redirects, namesByStat, stat, index, Links, origins);

        public References With(AppliedLinks held) => new(types, names, redirects, namesByStat, stat, Changes, held, origins);

        public References With(
            IReadOnlyDictionary<(EntityType Type, int Id), IReadOnlyList<(EntityType Type, int Id)>> roots) =>
            new(types, names, redirects, namesByStat, stat, Changes, Links, roots);

        /// <summary>
        /// Effects the abilities in <paramref name="granted"/> apply or have as parts, in grant order, after
        /// <paramref name="own"/>.
        /// </summary>
        public IReadOnlyList<EntityReference>? Through(IReadOnlyList<EntityReference>? granted, IReadOnlyList<EntityReference>? own = null) =>
            Distinct((own ?? []).Concat((granted ?? []).SelectMany(ability =>
                (Links.Applied(ability) ?? []).Concat((Links.Parts(ability) ?? []).Where(part => part.Type == EntityType.Effect)))));

        public IReadOnlyList<EntityReference>? Origins(Entity entity) =>
            entity.Type is { } type && entity.NetId is { } id ? Origins(type, id) : null;

        public IReadOnlyList<EntityReference>? Origins(EntityType type, int id) =>
            origins is not null && origins.TryGetValue((type, id), out var roots)
                ? [.. roots.Select(root => Of(root.Type, root.Id)!)]
                : null;

        public EntityReference? At(IconSource? source) =>
            source is null || types(source.Table, source.NetId) is not { } type
                ? null
                : Of(type, source.NetId);

        public EntityReference? Of(EntityType type, int? id)
        {
            if (id is not { } found) return null;

            var target = redirects is not null && redirects.TryGetValue((type, found), out var host) ? host : found;

            return new EntityReference(
                type,
                target,
                stat is not null && namesByStat is not null && namesByStat.TryGetValue((type, target, stat), out var named)
                    ? named
                    : names(type, target));
        }
    }

#pragma warning disable CS8524

    public static EntityDocument From(Entity entity, References references, EffectRoles roles)
    {
        var tuned = new Lazy<TunedRows>(() => Tuned(entity, references));

        return Projected(entity, references, roles, tuned);
    }

    private static EntityDocument Projected(
        Entity entity, References references, EffectRoles roles, Lazy<TunedRows> tuned) => entity.Type switch
    {
        EntityType.Ability => new AbilityDocument
        {
            FieldSources = Sourced<AbilityDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            NoKnownSource = entity.NoKnownSource,
            NamedAfter = references.At(entity.NamedAfter),
            NameBorrowedFrom = references.At(entity.NameInheritedFrom),
            PartOf = Name(entity) is null ? references.Changes.PartOf(entity, references) : null,
            ParentClass = Parent(entity, EntityType.Ability, references),
            Applies = references.Through(
                Granted(entity, references),
                references.Links.Applied(references.Of(EntityType.Ability, entity.NetId)) ?? Applied(entity, references)),
            Parts = references.Links.Parts(references.Of(EntityType.Ability, entity.NetId)),
            Grants = Granted(entity, references),
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Detail = entity.Detail,
            Icon = entity.Icon,
            Category = entity.Category is { } category ? Enum.Parse<AbilityCategory>(category) : null,
            CastTime = entity.CastTime,
            ChannelTime = entity.ChannelTime,
            ChannelTick = entity.ChannelTick,
            Cooldown = entity.Cooldown,
            GlobalCooldown = entity.GlobalCooldown,
            GlobalCooldownScalesWithHaste = entity.GlobalCooldownScalesWithHaste,
            Charges = entity.Charges,
            ChargesByLevelType = entity.ChargesByLevelType,
            TargetCountFalloff = Falloff(entity, references),
            MaxTargets = entity.MaxTargets,
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            CooldownAccelerationGrantedBy = Augmentations(entity.CooldownAccelerationGrantedBy, references),
            Cost = entity.Cost,
            Resource = entity.Resource,
            Generates = Generated(entity),
            Thresholds = entity.Thresholds,
            Range = entity.Range,
            MinRange = entity.MinRange,
            Radius = entity.Radius,
            Duration = entity.Duration,
            Period = entity.Period,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            CommitTriggers = Triggers(entity, references),
            Coefficients = entity.Coefficients,
            SetByCallerScaling = entity.SetByCallerScaling,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            Schools = Schools(entity.Tags),
            Heroes = Owning(entity),
            Owner = references.At(entity.Owner),
            Origins = references.Origins(entity),
            ChangedBy = references.Changes.ChangedBy(entity, references),
        },

        EntityType.Item => new ItemDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
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
            AttunementTree = entity.AttunementTree,
            Stats = entity.Stats,
            Description = entity.Description,
            Acquire = entity.Acquire,
            TitleText = entity.TitleText,
            Icon = entity.Icon,
            IconByRarity = entity.IconByRarity,
            Grants = references.At(Augmentation(entity.GrantsId)),
            LegendaryStats = Referenced(
                entity.LegendaryStatIds?.Select(id => Augmentation(id)).OfType<IconSource>().ToArray(), references),
            UntaggedLegendaryStats = entity.UntaggedLegendaryStats,
            Sources = Sourced(entity.Sources, references),
            ItemLevel = entity.ItemLevel,
        },

        EntityType.Gem => new GemDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Type = Enum.Parse<GemType>(entity.GemType!),
            Power = entity.GemPower!.Value,

            Rarity = (Rarity)entity.RarityTier!.Value,

            Icon = entity.Icon,
        },

        EntityType.TalentSlot => new TalentSlotDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Hero = Enum.Parse<Hero>(entity.Heroes!.Single()),
            Row = entity.TalentRow!.Value,
            Column = entity.TalentColumn!.Value,
            Cost = entity.TalentCost!.Value,

            Talent = references.Of(EntityType.Talent, entity.Augmentation ?? entity.NetId!.Value)!,
        },

        EntityType.Effect => new EffectDocument
        {
            FieldSources = Sourced<EffectDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            NoKnownSource = entity.NoKnownSource,
            Description = entity.Description,
            PrimaryStat = Stat(entity),
            EffectTags = entity.EffectTags,
            Icon = entity.Icon,
            Duration = entity.Duration,
            DurationScaling = entity.DurationScaling,
            Period = entity.Period,
            Cooldown = entity.Cooldown,
            Range = entity.Range,
            Radius = entity.Radius,
            Cost = entity.Cost,
            TickrateIgnoresHaste = entity.TickrateIgnoresHaste,
            CalculationModifiers = entity.CalculationModifiers,
            MaxStacks = entity.MaxStacks,
            Ticks = entity.Ticks,
            Infinite = entity.Infinite,
            ScalesOff = entity.ScalesOff,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            Schools = Schools(entity.Tags) ?? Schools(entity.ApplierTags),
            Modifiers = Stacked(entity.Modifiers, entity.MaxStacks),
            Generates = Generated(entity),
            Thresholds = entity.Thresholds,
            Talents = entity.Talents,
            NameBorrowedFrom = references.At(entity.NameInheritedFrom),
            TextureBorrowedFrom = references.At(entity.IconInheritedFrom),
            Heroes = Owning(entity),
            Owner = references.At(entity.Owner),
            Origins = references.Origins(entity),
            AppliedBy = references.At(entity.AppliedBy),
            LinkedBy = Linked(entity, references),
            AppliedOnCommitOf = Commits(entity, references),
            PartOf = references.Of(EntityType.Ability, roles.PartOf(entity.Name)),
            Siblings = references.Links.Siblings(references.Of(EntityType.Effect, entity.NetId)),
            ParentClass = Parent(entity, EntityType.Effect, references),
            Applies = Applied(entity, references),
            Grants = references.Changes.Named(entity, entity.GrantedAbilityNames, EntityType.Ability, references),
            Role = roles.Role(entity.Name),
            ChangedBy = references.Changes.ChangedBy(entity, references),
            Changes = references.Changes.Of(entity),
        },

        EntityType.Creature => new CreatureDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
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
            Abilities = Abilities(entity.Abilities, references),
            Passives = Abilities(entity.Passives, references),
            FoundIn = Referenced(entity.FoundIn, references),
        },

        EntityType.Dungeon => new DungeonDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Background = entity.Background,
            MaxPlayers = entity.MaxPlayers,
            QualifierTime = entity.QualifierTime,
            RequiredScore = entity.RequiredScore,
            MinimumHeroRank = entity.MinimumHeroRank,
            LevelType = entity.LevelType,
            SeasonalPinnacle = entity.SeasonalPinnacle,
            AvailableForCasual = entity.AvailableForCasual,
            MinimumDifficulty = entity.MinimumDifficulty,
            Difficulties = entity.Difficulties,
            Floors = entity.Floors,
            Placements = entity.Placements?.Select(placed => references.At(placed.Creature) is { } creature
                    ? new DungeonPlacement(creature, placed.Position)
                    {
                        Floor = placed.Floor,
                        MapX = placed.MapX,
                        MapY = placed.MapY,
                        Patrol = placed.Patrol,
                    }
                    : null)
                .OfType<DungeonPlacement>()
                .ToArray() is { Length: > 0 } placements
                ? placements
                : null,
            Patrols = entity.Patrols,
        },

        EntityType.DungeonAffix => new DungeonAffixDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ProcRolls = Rolled(entity, references),
            Icon = entity.Icon,
            Generates = Generated(entity),
            MinimumHeroRank = entity.MinimumHeroRank,
            Applies = references.Through(
                references.Changes.Named(entity, entity.GrantedAbilityNames, EntityType.Ability, references),
                references.Changes.Named(entity, entity.ReachedEffects, EntityType.Effect, references)),
            Grants = references.Changes.Named(entity, entity.GrantedAbilityNames, EntityType.Ability, references),
        },

        EntityType.Hero => new HeroDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Title = entity.HeroTitle,
            MainStat = entity.PrimaryStat is { } mainStat ? Enum.Parse<PrimaryStat>(mainStat) : null,
            Color = entity.HeroColor,
            Biography = entity.Biography,
            Playstyle = entity.Playstyle,
            SpiritAbility = entity.SpiritAbility,
            SpiritAbilityReferences = Referenced(entity.SpiritAbilityReferences, references),
            Portrait = entity.Icon,
            Role = entity.Role,
            RoleIcon = entity.RoleIcon,
            RoleOrdinal = entity.RoleOrdinal,
            OverallDifficulty = entity.OverallDifficulty,
            OverallDifficultyStars = entity.OverallDifficultyStars,
            Offense = entity.Offense,
            Defense = entity.Defense,
            Control = entity.Control,
            BaseAttributes = entity.BaseAttributes,
        },

        EntityType.Mount => new MountDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Lore = entity.Lore,
            Icon = entity.Icon,
            Rarity = entity.RarityTier is { } mountTier ? (Rarity)mountTier : null,
            Acquire = entity.Acquire,
        },

        EntityType.Currency => new CurrencyDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Rarity = entity.RarityTier is { } currencyTier ? (Rarity)currencyTier : null,
            Description = entity.Description,
            Icon = entity.Icon,
        },

        EntityType.CompanionPet => new CompanionPetDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Acquire = entity.Acquire,
            Icon = entity.Icon,
        },

        EntityType.MountPower => new MountPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.Radiance => new RadianceDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Type = Enum.Parse<GemType>(entity.GemType!),
            Attribute = entity.GemAttribute!,
            Icon = entity.GemOverflow?.Icon,
            OverflowDescription = entity.GemOverflow?.Description,
            OverflowPlaceholders = Spent(entity.GemOverflow?.Description, entity.GemOverflow?.Placeholders),
        },

        EntityType.GemAbility => new GemAbilityDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Gem = Enum.Parse<GemType>(entity.GemType!),
            Node = entity.GemNode!.Value,
            RequiredPower = entity.RequiredGemPower!.Value,
            Replaces = references.Of(EntityType.GemAbility, entity.Replaces),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
        },

        EntityType.Talent => new TalentDocument
        {
            FieldSources = Sourced<TalentDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Range = entity.Range,
            Radius = entity.Radius,
            Cost = entity.Cost,
            TargetCountFalloff = Falloff(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
            Changes = references.Changes.Of(entity),
            Constants = references.Changes.Constants(entity),
            Origins = references.Origins(entity),
        },

        EntityType.Glyph => new GlyphDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            MaxRank = entity.AbilityMaxRank,
            Slots = entity.PoolSlots,
            AbilityRank = entity.AbilityRank,
            IconHovered = entity.IconHovered,
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Constants = entity.ConstantsByRank,
            Generates = Generated(entity),
        },

        EntityType.Sigil => new SigilDocument
        {
            FieldSources = Sourced<SigilDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Category = entity.AugmentationCategory,
            CategoryTag = entity.AugmentationCategoryTag,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            RankRuns = entity.RankRuns,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.ItemTrait => new ItemTraitDocument
        {
            FieldSources = Sourced<ItemTraitDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Rollable = entity is { RandomChance: > 0, AugmentationCategoryTag: not null },
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            RankRuns = entity.RankRuns,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.Aspect => new AspectDocument
        {
            FieldSources = Sourced<AspectDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Category = entity.AugmentationCategory,
            CategoryTag = entity.AugmentationCategoryTag,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            RankRuns = entity.RankRuns,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.ProtectiveEnchant => new ProtectiveEnchantDocument
        {
            FieldSources = Sourced<ProtectiveEnchantDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Category = entity.AugmentationCategory,
            CategoryTag = entity.AugmentationCategoryTag,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            RankRuns = entity.RankRuns,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.Enchant => new EnchantDocument
        {
            FieldSources = Sourced<EnchantDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Category = entity.AugmentationCategory,
            CategoryTag = entity.AugmentationCategoryTag,
            MaxRank = entity.MaxRank,
            RandomChance = entity.RandomChance,
            Description = entity.Description,
            DescriptionsByHero = entity.DescriptionsByHero,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            RankRuns = entity.RankRuns,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
            PrimaryStat = Stat(entity),
            Stats = entity.EnchantStats,
        },

        EntityType.RelicPower => new RelicPowerDocument
        {
            FieldSources = Sourced<RelicPowerDocument>(entity, null),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            Cooldown = entity.Cooldown,
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            CooldownAccelerationGrantedBy = Augmentations(entity.CooldownAccelerationGrantedBy, references),
            Duration = entity.Duration,
            Range = entity.Range,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            Schools = Schools(entity.Tags),
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.WeaponPower => new WeaponPowerDocument
        {
            FieldSources = Sourced<WeaponPowerDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            CastTime = entity.CastTime,
            ChannelTime = entity.ChannelTime,
            ChannelTick = entity.ChannelTick,
            Cooldown = entity.Cooldown,
            CooldownDurationScalesOff = entity.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = entity.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = Augmentations(entity.CooldownHasteGrantedBy, references),
            CooldownAccelerationGrantedBy = Augmentations(entity.CooldownAccelerationGrantedBy, references),
            Duration = entity.Duration,
            Period = entity.Period,
            Range = entity.Range,
            Radius = entity.Radius,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            Schools = Schools(entity.Tags),
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.LegendaryPower => new LegendaryPowerDocument
        {
            FieldSources = Sourced<LegendaryPowerDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
            Changes = references.Changes.Of(entity),
            Constants = references.Changes.Constants(entity),
        },

        EntityType.RelicPassive => new RelicPassiveDocument
        {
            FieldSources = Sourced<RelicPassiveDocument>(entity, tuned.Value),
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = tuned.Value.Tuning,
            ScalesOff = entity.ScalesOff,
            ProcRolls = Rolled(entity, references),
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
            Generates = Generated(entity),
        },

        EntityType.LegendaryStat => new LegendaryStatDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Applies = references.Through(Referenced(entity.Granted, references)),
        },

        null => throw new InvalidOperationException($"{entity.Name} has no type and should not have been projected."),
    };
#pragma warning restore CS8524

    public static EntityDocument? Implied(Entity entity, References? references = null) =>
        entity is { Type: EntityType.TalentSlot, SlotBorneTalent: true }
            ? new TalentDocument
            {
                Id = entity.NetId!.Value,
                Name = Name(entity),
                Description = entity.Description,
                Icon = entity.Icon,
                Origins = references?.Origins(EntityType.Talent, entity.NetId!.Value),
            }
            : null;

    public static string? Name(Entity entity) =>
        entity.DisplayName is { } name && ReadsAsWords(name) ? name : null;

    /// <summary>
    /// Links an ability's document has from the documents naming it. An effect naming the ability as its applier is
    /// one the ability applies. An effect or ability part of it, or named after it, is a part. An unnamed ability
    /// whose applied effects are all part of one ability is a part of that one. An ability granted by
    /// an effect the ability applies is one it grants.
    /// </summary>
    public static AppliedLinks Links(IEnumerable<Entity> entities, References references, EffectRoles roles)
    {
        var links = new AppliedLinks();
        var grantedBy = new Dictionary<(EntityType Type, int Id), IReadOnlyList<EntityReference>>();
        var owners = new Dictionary<(EntityType Type, int Id), EntityReference>();
        var helpers = new List<(EntityReference Ability, IReadOnlyList<EntityReference> Effects)>();

        foreach (var entity in entities)
        {
            if (entity is { Type: EntityType.Ability, NetId: { } ability } &&
                references.Of(EntityType.Ability, ability) is { } self)
            {
                var applied = Applied(entity, references) ?? [];

                foreach (var effect in applied) links.Apply(self, effect);

                if (references.At(entity.NamedAfter) is { Type: EntityType.Ability } named) links.Part(named, self);

                if (Name(entity) is null)
                {
                    if (references.Changes.PartOf(entity, references) is { } whole) links.Part(whole, self);
                    else if (applied.Count > 0) helpers.Add((self, applied));
                }
            }

            if (entity is not { Type: EntityType.Effect, NetId: { } id } ||
                references.Of(EntityType.Effect, id) is not { } reached)
            {
                continue;
            }

            if (references.At(entity.AppliedBy) is { Type: EntityType.Ability } applier)
            {
                links.Apply(applier, reached);
                links.Child(applier, reached);
            }

            if (references.Of(EntityType.Ability, roles.PartOf(entity.Name)) is { } owner)
            {
                links.Part(owner, reached);
                links.Child(owner, reached);
                owners[(reached.Type, reached.Id)] = owner;
            }

            if (references.Changes.Named(entity, entity.GrantedAbilityNames, EntityType.Ability, references) is { } granted)
            {
                grantedBy[(reached.Type, reached.Id)] = granted;
            }
        }

        links.GrantThroughEffects(grantedBy);

        foreach (var (helper, effects) in helpers)
        {
            if (effects.Select(effect => owners.GetValueOrDefault((effect.Type, effect.Id))).ToArray() is { } held &&
                held.All(owner => owner is not null) &&
                held.DistinctBy(owner => owner!.Id).ToArray() is [{ } whole] &&
                whole.Id != helper.Id)
            {
                links.Part(whole, helper);
            }
        }

        return links;
    }

    /// <summary>Abilities the ability grants, its own and those its applied effects grant.</summary>
    private static IReadOnlyList<EntityReference>? Granted(Entity ability, References references) =>
        Distinct(
            (references.Changes.Named(ability, ability.GrantedAbilityNames, EntityType.Ability, references) ?? [])
            .Concat(references.Links.Granted(references.Of(EntityType.Ability, ability.NetId)) ?? []));

    private static IReadOnlyList<EntityReference>? Distinct(IEnumerable<EntityReference> references) =>
        references.DistinctBy(reference => (reference.Type, reference.Id)).ToArray() is { Length: > 0 } distinct
            ? distinct
            : null;

    /// <summary>Effects the entity references, other than its cost, cooldown and charge effects.</summary>
    private static IReadOnlyList<EntityReference>? Applied(Entity entity, References references) =>
        references.Changes.Named(
            entity,
            entity.ReachedEffects?.Except(entity.TimingEffects ?? [], StringComparer.OrdinalIgnoreCase),
            EntityType.Effect,
            references);

    private static EntityReference? Parent(Entity entity, EntityType type, References references) =>
        references.Changes.Named(entity, entity.ParentClassName is { } parent ? [parent] : null, type, references) is [var only]
            ? only
            : null;

    private static PrimaryStat? Stat(Entity entity) =>
        entity.PrimaryStat is { } stat ? Enum.Parse<PrimaryStat>(stat) : null;

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
                {
                    EnabledByDefault = slot.EnabledByDefault,
                    InitialCooldown = slot.InitialCooldown,
                    Cooldown = slot.Cooldown,
                    InterruptCooldown = slot.InterruptCooldown,
                    CCCooldown = slot.CCCooldown,
                    PostAbilityPadding = slot.PostAbilityPadding,
                }
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

    /// <summary>
    /// Every row the entity's placeholders, changes, talent groups and own constants read, with its value where no
    /// modifier applies it. A row a modifier applies has the attribute and op without the value, so a document shows a
    /// modifier's value only through the modifier or change applying it. A row of the
    /// entity's own constants that another entity owns is on that entity's document instead, unless the row sits under
    /// the entity's own namespace. A row of the entity's own constants that a talent group also reads is under the
    /// group's name alone, and a row a placeholder reads under a longer key has the attribute and op the group's
    /// gated modifier applies it under.
    /// </summary>
    private static TunedRows Tuned(Entity entity, References references)
    {
        var tuned = new SortedDictionary<string, TuningReference>(StringComparer.Ordinal);
        var named = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        void Name(string key, string? read = null)
        {
            if (!named.TryGetValue(key, out var held)) named[key] = held = new(StringComparer.OrdinalIgnoreCase);

            held.Add(key);
            held.Add(references.Changes.Row(key, entity));
            if (read is not null) held.Add(read);

            if (entity.Tuning is { } scope && $"{scope}.{key}" is var under &&
                (entity.Rows?.ContainsKey(under) is true || entity.NamespaceRows?.ContainsKey(under) is true))
            {
                held.Add(under);
            }
        }

        var rows = (entity.Placeholders?.Values ?? [])
            .SelectMany(placeholder => placeholder.Constants ?? new Dictionary<string, object?>())
            .Concat((references.Changes.Constants(entity) ?? new Dictionary<string, double>())
                .Select(row => KeyValuePair.Create(row.Key, (object?)row.Value)));

        foreach (var (row, value) in rows)
        {
            if (!tuned.ContainsKey(row) && references.Changes.Reference(row, entity, references) is { } reference)
            {
                tuned[row] = reference with { Value = value };
                Name(row);
            }
        }

        var constants = new List<string>();

        foreach (var (row, value) in entity.Constants ?? new Dictionary<string, object?>())
        {
            if (value is null || tuned.ContainsKey(row)) continue;

            var own = references.Changes.Reference(row, entity, references) ??
                      references.Changes.Referenced(row, entity, references);

            if (own.Owner is not null && !Under(entity, row)) continue;

            tuned[row] = own with { Value = value };
            Name(row);
            constants.Add(row);
        }

        foreach (var (group, leaves) in entity.Talents ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
        {
            foreach (var (leaf, value) in leaves)
            {
                var relative = $"{group}.{leaf}";
                var row = (entity.Rows?.Keys ?? []).FirstOrDefault(key =>
                    key.EndsWith("." + relative, StringComparison.OrdinalIgnoreCase));

                if (row is not null && references.Changes.Reference(row, entity, references) is { } reference)
                {
                    tuned[relative] = reference with { Value = value };
                    Name(relative, row);
                }
                else if (Gate(entity, group) is { } gate && references.Changes.Holding(gate) is [var (type, id)])
                {
                    var applied = (entity.GatedModifiers?.GetValueOrDefault(gate) ?? [])
                        .Where(modifier => modifier is { Op: not null, Row: { } read } &&
                                           read.EndsWith("." + relative, StringComparison.OrdinalIgnoreCase))
                        .Select(modifier => (modifier.Attribute, Op: modifier.Op!, Row: modifier.Row!))
                        .DistinctBy(modifier => (modifier.Attribute, modifier.Op))
                        .ToArray();

                    var (attribute, op, read) = applied is [var single] ? single : default;

                    Name(relative, read);
                    tuned[relative] = new TuningReference(ModifierIndex.Fold(leaf))
                    {
                        Talent = references.Of(type, id),
                        Value = value,
                        Attribute = attribute,
                        Op = op,
                        Percent = read is not null && ModifierOps.Adds(op) && references.Changes.Percent(read, type, id)
                            ? true
                            : null,
                    };
                }
                else if (references.Changes.TalentNamed(entity, group, references) is { } talent)
                {
                    tuned[relative] = new TuningReference(ModifierIndex.Fold(leaf)) { Talent = talent, Value = value };
                    Name(relative);
                }
            }
        }

        var grouped = (entity.Talents ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
            .SelectMany(group => group.Value.Keys.Select(leaf => $"{group.Key}.{leaf}"))
            .Where(tuned.ContainsKey)
            .ToArray();

        foreach (var row in constants)
        {
            if (grouped.Any(relative => relative != row && row.EndsWith("." + relative, StringComparison.OrdinalIgnoreCase)))
            {
                tuned.Remove(row);
            }
        }

        foreach (var relative in grouped)
        {
            if (tuned[relative] is not { Op: not null } applied) continue;

            foreach (var (row, reference) in tuned.Where(entry => entry.Key != relative && entry.Value.Op is null &&
                         entry.Value.Name == applied.Name &&
                         entry.Key.EndsWith("." + relative, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                tuned[row] = reference with { Attribute = applied.Attribute, Op = applied.Op, Percent = applied.Percent };
            }
        }

        var keys = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in tuned.Keys)
        {
            foreach (var row in named.GetValueOrDefault(key) ?? [])
            {
                if (!keys.TryGetValue(row, out var naming)) keys[row] = naming = [];
                naming.Add(key);
            }
        }

        return new TunedRows(tuned.Count == 0 ? null : tuned, keys);
    }

    /// <summary>A document's <c>tuning</c>, with the keys of it naming each constants row.</summary>
    private sealed record TunedRows(
        IReadOnlyDictionary<string, TuningReference>? Tuning, IReadOnlyDictionary<string, List<string>> Keys);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, HashSet<string>> MembersByDocument = new();

    /// <summary>
    /// The entity's field sources for the fields <typeparamref name="TDocument"/> has, each with the keys of the
    /// document's <c>tuning</c> naming its rows.
    /// </summary>
    private static IReadOnlyDictionary<string, FieldSource>? Sourced<TDocument>(Entity entity, TunedRows? tuned)
        where TDocument : EntityDocument
    {
        if (entity.FieldSources is not { Count: > 0 } sources) return null;

        var members = MembersByDocument.GetOrAdd(typeof(TDocument), type =>
            [.. type.GetProperties().Select(property => FieldSources.Key(property.Name))]);

        var projected = new SortedDictionary<string, FieldSource>(StringComparer.Ordinal);

        foreach (var (field, source) in sources)
        {
            if (!members.Contains(field)) continue;

            var keys = source.Rows
                .SelectMany(row => tuned?.Keys.GetValueOrDefault(row) ?? [])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            projected[field] = keys.Length == 0 ? source : source with { Tuning = keys };
        }

        return projected.Count == 0 ? null : projected;
    }

    /// <summary>True when the row sits under the entity's own namespace, which another entity can share.</summary>
    private static bool Under(Entity entity, string row) =>
        entity.Tuning is { } scope && entity.Rows?.ContainsKey($"{scope}.{row}") == true;

    private static string? Gate(Entity entity, string group) =>
        entity.TalentGates?.GetValueOrDefault(group) ??
        (entity.GatedModifiers?.Keys ?? []).FirstOrDefault(tag => tag.EndsWith("." + group, StringComparison.Ordinal));

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

    /// <summary>
    /// Each modifier with its value at the effect's maximum stacks. Massacre's Spirit 0.0025 is 0.25 at its 100
    /// stacks, the 25% its talent prints.
    /// </summary>
    private static IReadOnlyList<EffectModifier>? Stacked(IReadOnlyList<EffectModifier>? modifiers, int? maxStacks)
    {
        if (modifiers is null || maxStacks is not { } stacks || stacks <= 1) return modifiers;

        return [.. modifiers.Select(modifier => modifier is { Value: { } value, Op: { } op } &&
                                                 AtStacks(op, value, stacks) is { } total && double.IsFinite(total)
            ? modifier with { AtMaxStacks = Math.Round(total, 4) }
            : modifier)];
    }

    private static double? AtStacks(string op, double value, int stacks) => op switch
    {
        "Additive" => value * stacks,
        "Multiplicitive" => 1 + (value - 1) * stacks,
        "MultiplyCompound" => Math.Pow(value, stacks),
        _ => null,
    };

    private static IReadOnlyList<ResourceGain>? Generated(Entity entity) =>
        entity.Generates is { Count: > 0 } gains ? gains : null;

    /// <summary>
    /// The entity's proc rolls for its document. A roll behind a tag names the talent or legendary power holding the
    /// tag, and a roll behind a tag nothing holds is left out. A talent or legendary power also carries the rolls
    /// abilities make only behind a tag it holds.
    /// </summary>
    private static IReadOnlyList<ProcRoll>? Rolled(Entity entity, References references)
    {
        var rolls = new List<ProcRoll>();

        foreach (var roll in entity.ProcRolls ?? [])
        {
            if (roll.Gates is not { Count: > 0 } gates)
            {
                rolls.Add(roll);
                continue;
            }

            var holders = gates
                .SelectMany(gate => references.Changes.Holding(gate))
                .Distinct()
                .ToArray();

            if (holders.Any(holder => holder.Type == entity.Type && holder.Id == entity.NetId))
            {
                rolls.Add(roll with { Gates = null });
                continue;
            }

            var talent = holders.Select(holder => references.Of(holder.Type, holder.Id)).OfType<EntityReference>().FirstOrDefault();

            if (talent is not null) rolls.Add(roll with { Gates = null, Talent = talent });
        }

        foreach (var roll in references.Changes.GatedRolls(entity))
        {
            var opened = roll with { Gates = null };
            if (!rolls.Any(held => ProcRolls.Same(held, opened))) rolls.Add(opened);
        }

        return rolls.Count == 0 ? null : rolls;
    }

    private const string AbilityTypeTagPrefix = "AbilityType.";
    private const string EventTagPrefix = "Event.";

    /// <summary>A wait's abilities, or its tags where they stand for abilities at large or name none.</summary>
    private static CommitTrigger Trigger(CommitTriggerSource source, References references)
    {
        bool Broad(string tag) =>
            tag.StartsWith(AbilityTypeTagPrefix, StringComparison.Ordinal) || tag.StartsWith(EventTagPrefix, StringComparison.Ordinal);

        int[] committing =
        [
            .. source.Tags.Where(tag => !Broad(tag)).SelectMany(tag => references.Changes.AbilitiesTagged(tag)).Distinct().Order(),
        ];

        var unresolved = source.Tags.Where(tag => Broad(tag) || references.Changes.AbilitiesTagged(tag).Count == 0).ToArray();

        EntityReference? Single(IReadOnlyList<string> tags) => tags
            .SelectMany(tag => references.Changes.Holding(tag))
            .Select(holder => references.Of(holder.Type, holder.Id))
            .OfType<EntityReference>()
            .FirstOrDefault();

        var stoppedBy = Single(source.Stopping);
        var talent = Single(source.Required);

        return new CommitTrigger
        {
            Committing = committing.Length == 0
                ? null
                : [.. committing.Select(id => references.Of(EntityType.Ability, id)).OfType<EntityReference>().Distinct()],
            CommittingTag = unresolved.Length > 0 ? string.Join(", ", unresolved) : null,
            CommittingHoldsAll = source.AllTags ? true : null,
            ExcludedTags = source.Excluded.Count > 0 ? source.Excluded : null,
            Applies = source.Effects.Count == 0 ? null : [.. source.Effects.Select(references.At).OfType<EntityReference>()],
            StoppedBy = stoppedBy,
            Talent = talent,
            StoppedByTags = stoppedBy is null && source.Stopping.Count > 0 ? source.Stopping : null,
            RequiredTags = talent is null && source.Required.Count > 0 ? source.Required : null,
        };
    }

    private static IReadOnlyList<CommitTrigger>? Triggers(Entity entity, References references) =>
        entity.CommitTriggers is { Count: > 0 } triggers
            ? [.. triggers.Select(trigger => Trigger(trigger, references))]
            : null;

    private static IReadOnlyList<CommitTrigger>? Commits(Entity entity, References references) =>
        references.Changes.CommitsOf(entity) is { Count: > 0 } commits
            ? [
                .. commits.Select(commit => Trigger(commit.Trigger, references) with
                {
                    Ability = references.Of(EntityType.Ability, commit.Ability.NetId),
                    Applies = null,
                }),
            ]
            : null;

    /// <summary>A link names the talent holding a tag it needs, as a proc roll does.</summary>
    private static IReadOnlyList<EffectLink>? Linked(Entity entity, References references)
    {
        if (entity.LinkedBy is not { Count: > 0 } links) return null;

        var linked = new List<EffectLink>();

        foreach (var link in links)
        {
            if (references.At(link.Parent) is not { } parent) continue;

            var talent = (link.RequiredSourceTags ?? [])
                .SelectMany(tag => references.Changes.Holding(tag))
                .Select(holder => references.Of(holder.Type, holder.Id))
                .OfType<EntityReference>()
                .FirstOrDefault();

            linked.Add(new EffectLink(parent, link.ApplyTo)
            {
                Talent = talent,
                RequiredSourceTags = talent is null ? link.RequiredSourceTags : null,
                RequiredTargetTags = link.RequiredTargetTags,
                BannedSourceTags = link.BannedSourceTags,
            });
        }

        return linked.Count == 0 ? null : linked;
    }

    private static IReadOnlyList<TargetCountFalloff>? Falloff(Entity entity, References references) =>
        entity.TargetCountFalloff?
            .Select(source => new TargetCountFalloff
            {
                Effect = references.At(source.Effect),
                Threshold = source.Threshold,
            })
            .Distinct()
            .ToArray();
}
