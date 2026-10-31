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
        ChangeIndex? changes = null)
    {
        public ChangeIndex Changes { get; } = changes ?? ChangeIndex.None;

        public References For(string? primaryStat) => new(types, names, redirects, namesByStat, primaryStat, Changes);

        public References With(ChangeIndex index) => new(types, names, redirects, namesByStat, stat, index);

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

    public static EntityDocument From(Entity entity, References references, EffectRoles roles) => entity.Type switch
    {
        EntityType.Ability => new AbilityDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            NamedAfter = references.At(entity.NamedAfter),
            PartOf = Name(entity) is null ? references.Changes.PartOf(entity, references) : null,
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
            Range = entity.Range,
            MinRange = entity.MinRange,
            Radius = entity.Radius,
            Duration = entity.Duration,
            Period = entity.Period,
            CastTimeScalesWithHaste = entity.CastTimeScalesWithHaste,
            ScalesOff = entity.ScalesOff,
            Coefficients = entity.Coefficients,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            Schools = Schools(entity.Tags),
            Heroes = Owning(entity),
            Owner = references.At(entity.Owner),
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
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            PrimaryStat = Stat(entity),
            EffectTags = entity.EffectTags,
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
            Tuning = Tuned(entity, references),
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
            ChangedBy = references.Changes.ChangedBy(entity, references),
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
            Tuning = Tuned(entity, references),
            Icon = entity.Icon,
            Generates = Generated(entity),
            MinimumHeroRank = entity.MinimumHeroRank,
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
            OverallDifficulty = entity.OverallDifficulty,
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
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
        },

        EntityType.Talent => new TalentDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Cooldown = entity.Cooldown,
            Duration = entity.Duration,
            Range = entity.Range,
            Radius = entity.Radius,
            Cost = entity.Cost,
            TargetCountFalloff = Falloff(entity, references),
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
            Changes = references.Changes.Of(entity),
            Constants = references.Changes.Constants(entity),
        },

        EntityType.Glyph => new GlyphDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            MaxRank = entity.AbilityMaxRank,
            Slots = entity.PoolSlots,
            AbilityRank = entity.AbilityRank,
            IconHovered = entity.IconHovered,
            Grants = Referenced(entity.Granted, references),
            Constants = entity.ConstantsByRank,
            Generates = Generated(entity),
        },

        EntityType.Sigil => new SigilDocument
        {
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
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
        },

        EntityType.Aspect => new AspectDocument
        {
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
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
        },

        EntityType.ProtectiveEnchant => new ProtectiveEnchantDocument
        {
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
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
        },

        EntityType.Enchant => new EnchantDocument
        {
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
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
            PrimaryStat = Stat(entity),
            Stats = entity.EnchantStats,
        },

        EntityType.RelicPower => new RelicPowerDocument
        {
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
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
        },

        EntityType.WeaponPower => new WeaponPowerDocument
        {
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
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
        },

        EntityType.LegendaryPower => new LegendaryPowerDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
            Generates = Generated(entity),
            Changes = references.Changes.Of(entity),
            Constants = references.Changes.Constants(entity),
        },

        EntityType.RelicPassive => new RelicPassiveDocument
        {
            Id = entity.NetId!.Value,
            Name = Name(entity),
            EnabledInGame = entity.EnabledInGame,
            Description = entity.Description,
            Icon = entity.Icon,
            Cooldown = entity.Cooldown,
            Placeholders = Spent(Spends(entity), entity.Placeholders),
            Tuning = Tuned(entity, references),
            ScalesOff = entity.ScalesOff,
            Grants = Referenced(entity.Granted, references),
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

    private static IReadOnlyDictionary<string, TuningReference>? Tuned(Entity entity, References references)
    {
        var tuned = new SortedDictionary<string, TuningReference>(StringComparer.Ordinal);

        var rows = (entity.Placeholders?.Values ?? [])
            .SelectMany(placeholder => placeholder.Constants?.Keys ?? [])
            .Concat(references.Changes.Constants(entity)?.Keys ?? []);

        foreach (var row in rows)
        {
            if (!tuned.ContainsKey(row) && references.Changes.Reference(row, entity, references) is { } reference)
            {
                tuned[row] = reference;
            }
        }

        foreach (var (group, leaves) in entity.Talents ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
        {
            foreach (var leaf in leaves.Keys)
            {
                var relative = $"{group}.{leaf}";
                var row = (entity.Rows?.Keys ?? []).FirstOrDefault(key =>
                    key.EndsWith("." + relative, StringComparison.OrdinalIgnoreCase));

                if (row is not null && references.Changes.Reference(row, entity, references) is { } reference)
                {
                    tuned[relative] = reference;
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

                    tuned[relative] = new TuningReference(ModifierIndex.Fold(leaf))
                    {
                        Talent = references.Of(type, id),
                        Attribute = attribute,
                        Op = op,
                        Percent = read is not null && ModifierOps.Adds(op) && references.Changes.Percent(read, type, id)
                            ? true
                            : null,
                    };
                }
                else if (references.Changes.TalentNamed(entity, group, references) is { } talent)
                {
                    tuned[relative] = new TuningReference(ModifierIndex.Fold(leaf)) { Talent = talent };
                }
            }
        }

        return tuned.Count == 0 ? null : tuned;
    }

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

    private static IReadOnlyList<ResourceGain>? Generated(Entity entity) =>
        entity.Generates is { Count: > 0 } gains ? gains : null;

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
