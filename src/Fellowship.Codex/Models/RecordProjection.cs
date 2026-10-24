using System.Globalization;

using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public static class RecordProjection
{
    private static readonly IReadOnlyDictionary<EntityType, Func<EntityDocument, ICodexViewService, string, EntityRecord>>
        Table = Verified(new Dictionary<EntityType, Func<EntityDocument, ICodexViewService, string, EntityRecord>>
        {
            [EntityType.Ability] = (document, view, origin) => From((AbilityDocument)document, view, origin),
            [EntityType.Glyph] = (document, view, origin) => From((GlyphDocument)document, view, origin),
            [EntityType.CompanionPet] = (document, view, origin) => From((CompanionPetDocument)document, view, origin),
            [EntityType.Creature] = (document, view, origin) => From((CreatureDocument)document, view, origin),
            [EntityType.Currency] = (document, view, origin) => From((CurrencyDocument)document, view, origin),
            [EntityType.Dungeon] = (document, view, origin) => From((DungeonDocument)document, view, origin),
            [EntityType.Effect] = (document, view, origin) => From((EffectDocument)document, view, origin),
            [EntityType.Gem] = (document, view, origin) => From((GemDocument)document, view, origin),
            [EntityType.GemAbility] = (document, view, origin) => From((GemAbilityDocument)document, view, origin),
            [EntityType.Radiance] = (document, view, origin) => From((RadianceDocument)document, view, origin),
            [EntityType.Hero] = (document, view, origin) => From((HeroDocument)document, view, origin),
            [EntityType.Item] = (document, view, origin) => From((ItemDocument)document, view, origin),
            [EntityType.LegendaryPower] = (document, view, origin) =>
                From((LegendaryPowerDocument)document, view, origin),
            [EntityType.LegendaryStat] = (document, view, origin) =>
                From((LegendaryStatDocument)document, view, origin),
            [EntityType.Modifier] = (document, view, origin) => From((ModifierDocument)document, view, origin),
            [EntityType.Mount] = (document, view, origin) => From((MountDocument)document, view, origin),
            [EntityType.MountPower] = (document, view, origin) => From((MountPowerDocument)document, view, origin),
            [EntityType.RelicPower] = (document, view, origin) => From((RelicPowerDocument)document, view, origin),
            [EntityType.RelicPassive] = (document, view, origin) =>
                From((RelicPassiveDocument)document, view, origin),
            [EntityType.Talent] = (document, view, origin) => From((TalentDocument)document, view, origin),
            [EntityType.TalentSlot] = (document, view, origin) => From((TalentSlotDocument)document, view, origin),
            [EntityType.Enchant] = (document, view, origin) => From((EnchantDocument)document, view, origin),
            [EntityType.Sigil] = (document, view, origin) => From((SigilDocument)document, view, origin),
            [EntityType.Aspect] = (document, view, origin) => From((AspectDocument)document, view, origin),
            [EntityType.ProtectiveEnchant] = (document, view, origin) => From((ProtectiveEnchantDocument)document, view, origin),
            [EntityType.WeaponPower] = (document, view, origin) => From((WeaponPowerDocument)document, view, origin),
        });

    private static IReadOnlyDictionary<EntityType, Func<EntityDocument, ICodexViewService, string, EntityRecord>>
        Verified(IReadOnlyDictionary<EntityType, Func<EntityDocument, ICodexViewService, string, EntityRecord>> table)
    {
        var missing = Enum.GetValues<EntityType>().Except(table.Keys).ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "has" : "have")} no row in " +
                $"{nameof(RecordProjection)}.{nameof(Table)}, so the codex answers no record for " +
                (missing.Length == 1 ? "it." : "them."));
        }

        return table;
    }

    public static EntityRecord Of(EntityDocument document, ICodexViewService view, string origin) =>
        Table[EntityTypes.TypeOf(document)](document, view, origin);

    public static TRecord Of<TRecord>(EntityDocument document, ICodexViewService view, string origin)
        where TRecord : EntityRecord =>
        (TRecord)Of(document, view, origin);

    public static AbilityRecord From(AbilityDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];
        var tags = document.Schools ?? [];

        return new AbilityRecord
        {
            Type = EntityType.Ability,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            EnabledInGame = document.EnabledInGame,
            Description = view.PlainText(document.Description, tokens, tags),
            Detail = Lines(document.Detail, view, tokens, tags),
            Icon = Texture(document.Icon, view),
            Category = document.Category,
            CastTime = document.CastTime,
            ChannelTime = document.ChannelTime,
            ChannelTick = document.ChannelTick,
            Cooldown = document.Cooldown,
            Charges = document.Charges,
            ChargesByLevelType = document.ChargesByLevelType,
            TargetCountFalloff = Falloff(document.TargetCountFalloff, view, origin),
            MaxTargets = document.MaxTargets,
            CooldownDurationScalesOff = Scaling(document.CooldownDurationScalesOff, view),
            CooldownTimeRateScalesOff = Scaling(document.CooldownTimeRateScalesOff, view),
            CooldownHasteGrantedBy = References(document.CooldownHasteGrantedBy, view, origin),
            CooldownAccelerationGrantedBy = References(document.CooldownAccelerationGrantedBy, view, origin),
            GlobalCooldown = document.GlobalCooldown,
            GlobalCooldownScalesOff = Scaling(document.GlobalCooldownScalesOff, view),
            EffectTriggers = Some(document.EffectTriggers),
            ProcRolls = Some(document.ProcRolls),
            Cost = document.Cost,
            Resource = document.Resource,
            Range = document.Range,
            MinRange = document.MinRange,
            Radius = document.Radius,
            Duration = document.Duration,
            Period = document.Period,
            CastTimeScalesWithHaste = document.CastTimeScalesWithHaste,
            ScalesOff = Scaling(document.ScalesOff, view),
            Coefficients = Some(document.Coefficients),
            Placeholders = Tokens(tokens, tags, view),
            Schools = Some(tags),
            Heroes = Some(document.Heroes),
            Owner = Reference(document.Owner, view, origin),
        };
    }

    public static GlyphRecord From(GlyphDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return new GlyphRecord
        {
            Type = EntityType.Glyph,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, []),
            Icon = Texture(document.Icon, view),
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
            MaxRank = document.MaxRank,
            Slots = Some(document.Slots),
            AbilityRank = document.AbilityRank,
            Constants = document.Constants,
        };
    }

    public static CompanionPetRecord From(CompanionPetDocument document, ICodexViewService view, string origin) =>
        new()
        {
            Type = EntityType.CompanionPet,
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, [], []),
            Acquire = document.Acquire,
            Icon = Texture(document.Icon, view),
        };

    public static CreatureRecord From(CreatureDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Creature,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Icon = Texture(document.Icon, view),
        Rank = document.Rank,
        Health = document.Health,
        Armor = document.Armor,
        Resist = document.Resist,
        KillScore = document.KillScore,
        SpiritPoints = document.SpiritPoints,
        CountsTowardScore = document.CountsTowardScore,
        Intellect = document.Intellect,
        Strength = document.Strength,
        Agility = document.Agility,
        Scaling = document.Scaling,
        Abilities = Wielded(document.Abilities, view, origin),
        Passives = Wielded(document.Passives, view, origin),
        FoundIn = References(document.FoundIn, view, origin),
    };

    public static CurrencyRecord From(CurrencyDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Currency,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Rarity = view.Rarity(document),
        Description = view.PlainText(document.Description, [], []),
        Icon = Texture(document.Icon, view),
    };

    public static DungeonRecord From(DungeonDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Dungeon,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Description = view.PlainText(document.Description, [], []),
        Icon = Texture(document.Icon, view),
        Background = Texture(document.Background, view),
        MaxPlayers = document.MaxPlayers,
        QualifierTime = document.QualifierTime,
        RequiredScore = document.RequiredScore,
        MinimumHeroRank = document.MinimumHeroRank,
        LevelType = document.LevelType,
        SeasonalPinnacle = document.SeasonalPinnacle,
        MinimumDifficulty = document.MinimumDifficulty,
        Difficulties = document.Difficulties,
        Creatures = Dwelling(document, view, origin),
        Floors = document.Floors?.Select(floor => floor with { Map = Texture(floor.Map, view) }).ToArray(),
        Placements = document.Placements?.Select(placed => new DungeonPlacementRecord(
                Referenced(placed.Creature.Type, placed.Creature.Id, view, origin), placed.Position)
            {
                Floor = placed.Floor,
                MapX = placed.MapX,
                MapY = placed.MapY,
                Patrol = placed.Patrol,
            })
            .ToArray(),
        Patrols = document.Patrols,
    };

    public static EffectRecord From(EffectDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];
        var tags = document.Schools ?? [];

        return new EffectRecord
        {
            Type = EntityType.Effect,
            Id = document.Id,
            Generates = Generated(document.Generates),
            EffectTags = document.EffectTags,
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, tags),
            Icon = Texture(document.Icon, view),
            Duration = document.Duration,
            Period = document.Period,
            Cooldown = document.Cooldown,
            Range = document.Range,
            Radius = document.Radius,
            Cost = document.Cost,
            TickrateIgnoresHaste = document.TickrateIgnoresHaste,
            MaxStacks = document.MaxStacks,
            Stacking = document.Stacking,
            Infinite = document.Infinite,
            ScalesOff = Scaling(document.ScalesOff, view),
            Placeholders = Tokens(tokens, tags, view),
            Schools = Some(tags),
            Modifiers = Some(document.Modifiers),
            ExecutionModifiers = Some(document.ExecutionModifiers),
            Talents = document.Talents,
            NameBorrowedFrom = Reference(document.NameBorrowedFrom, view, origin),
            TextureBorrowedFrom = Reference(document.TextureBorrowedFrom, view, origin),
            Heroes = Some(document.Heroes),
            Owner = Reference(document.Owner, view, origin),
            AppliedBy = Reference(document.AppliedBy, view, origin),
            PartOf = Reference(document.PartOf, view, origin),
            Role = document.Role,
        };
    }

    public static GemRecord From(GemDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Gem,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        GemType = document.Type,
        Power = document.Power,
        Rarity = view.Rarity(document),
        Icon = Texture(document.Icon, view),
        GemOverflow = Overflow(document, view),
    };

    public static GemAbilityRecord From(GemAbilityDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return new GemAbilityRecord
        {
            Type = EntityType.GemAbility,
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            GemType = document.Gem,
            Node = document.Node,
            RequiredPower = document.RequiredPower,
            Replaces = Reference(document.Replaces, view, origin),
            Grants = References(document.Grants, view, origin),
            Description = view.PlainText(document.Description, tokens, []),
            Icon = Texture(document.Icon, view),
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
            GemOverflow = Overflow(document, view),
        };
    }

    public static RadianceRecord From(RadianceDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.OverflowPlaceholders ?? [];

        return new RadianceRecord
        {
            Type = EntityType.Radiance,
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            GemType = document.Type,
            Attribute = view.Store.StatName(document.Attribute),
            Icon = Texture(document.Icon, view),
            OverflowDescription = view.PlainText(document.OverflowDescription, tokens, []),
            OverflowPlaceholders = Tokens(tokens, [], view),
        };
    }

    public static HeroRecord From(HeroDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Hero,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Title = document.Title,
        MainStat = document.MainStat,
        Color = document.Color,
        Biography = document.Biography,
        Playstyle = document.Playstyle,
        SpiritAbility = document.SpiritAbility,
        SpiritAbilityReferences = References(document.SpiritAbilityReferences, view, origin),
        Portrait = Texture(document.Portrait, view),
        Role = document.Role,
        RoleIcon = Texture(document.RoleIcon, view),
        OverallDifficulty = document.OverallDifficulty,
        Offense = document.Offense,
        Defense = document.Defense,
        Control = document.Control,
        BaseAttributes = document.BaseAttributes,
    };

    public static ItemRecord From(ItemDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Item,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        ArmorType = document.ArmorType,
        PrimaryStat = document.PrimaryStat,
        Hero = document.Hero,
        Heroes = Some(document.Heroes),
        Slot = document.Slot,
        GemSlot = document.GemSlot,
        UniqueEquipped = document.UniqueEquipped,
        Rarity = view.Rarity(document),
        Rarities = RarityNames(document.Rarities, view),
        Stats = Slots(document, view),
        RuleSet = document.RuleSet,
        AttunementTree = Tree(document.AttunementTree, view, origin),
        Description = view.PlainText(document.Description, [], []),
        Acquire = document.Acquire,
        TitleText = document.TitleText,
        Icon = Texture(document.Icon, view),
        IconByRarity = IconsByRarity(document, view),
        Grants = Reference(document.Grants, view, origin),
        LegendaryStats = Rolls(document.LegendaryStats, view),
        UntaggedLegendaryStats = Some(document.UntaggedLegendaryStats),
        Sources = Sourced(document.Sources, view, origin),
        ItemLevel = document.ItemLevel,
    };

    public static LegendaryPowerRecord From(LegendaryPowerDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return new LegendaryPowerRecord
        {
            Type = EntityType.LegendaryPower,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, []),
            Icon = Texture(document.Icon, view),
            Cooldown = document.Cooldown,
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
        };
    }

    public static RelicPassiveRecord From(RelicPassiveDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return new RelicPassiveRecord
        {
            Type = EntityType.RelicPassive,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, []),
            Icon = Texture(document.Icon, view),
            Cooldown = document.Cooldown,
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
        };
    }

    public static LegendaryStatRecord From(LegendaryStatDocument document, ICodexViewService view, string origin) =>
        new()
        {
            Type = EntityType.LegendaryStat,
            Id = document.Id,
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, [], []),
            ScalesOff = Scaling(document.ScalesOff, view),
        };

    public static ModifierRecord From(ModifierDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Modifier,
        Id = document.Id,
        Generates = Generated(document.Generates),
        Name = PublicTypes.NameOf(document),
        Description = view.PlainText(document.Description, [], []),
        Icon = Texture(document.Icon, view),
        Kind = document.Kind,
        MinimumHeroRank = document.MinimumHeroRank,
    };

    public static MountRecord From(MountDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.Mount,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Description = view.PlainText(document.Description, [], []),
        Lore = document.Lore,
        Icon = Texture(document.Icon, view),
        Rarity = view.Rarity(document),
        Acquire = document.Acquire,
    };

    public static MountPowerRecord From(MountPowerDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.MountPower,
        Id = document.Id,
        Generates = Generated(document.Generates),
        Name = PublicTypes.NameOf(document),
        Description = view.PlainText(document.Description, [], []),
        Icon = Texture(document.Icon, view),
    };

    public static RelicPowerRecord From(RelicPowerDocument document, ICodexViewService view, string origin)
    {
        var tags = document.Schools ?? [];

        return new RelicPowerRecord
        {
            Type = EntityType.RelicPower,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, [], tags),
            Icon = Texture(document.Icon, view),
            CastTime = document.CastTime,
            Cooldown = document.Cooldown,
            CooldownDurationScalesOff = Scaling(document.CooldownDurationScalesOff, view),
            CooldownTimeRateScalesOff = Scaling(document.CooldownTimeRateScalesOff, view),
            CooldownHasteGrantedBy = References(document.CooldownHasteGrantedBy, view, origin),
            CooldownAccelerationGrantedBy = References(document.CooldownAccelerationGrantedBy, view, origin),
            Duration = document.Duration,
            Range = document.Range,
            CastTimeScalesWithHaste = document.CastTimeScalesWithHaste,
            Schools = Some(tags),
            ScalesOff = Scaling(document.ScalesOff, view),
        };
    }

    public static TalentRecord From(TalentDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return new TalentRecord
        {
            Type = EntityType.Talent,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, []),
            Icon = Texture(document.Icon, view),
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
            Cooldown = document.Cooldown,
            Duration = document.Duration,
            Range = document.Range,
            Radius = document.Radius,
            Cost = document.Cost,
            TargetCountFalloff = Falloff(document.TargetCountFalloff, view, origin),
        };
    }

    public static TalentSlotRecord From(TalentSlotDocument document, ICodexViewService view, string origin) => new()
    {
        Type = EntityType.TalentSlot,
        Id = document.Id,
        Name = PublicTypes.NameOf(document),
        Hero = document.Hero,
        Row = document.Row,
        Column = document.Column,
        Cost = document.Cost,
        Talent = Referenced(document.Talent.Type, document.Talent.Id, view, origin),
    };

    public static EnchantRecord From(EnchantDocument document, ICodexViewService view, string origin) =>
        (EnchantRecord)Augmentation(
            new EnchantRecord
            {
                Type = EntityType.Enchant,
                Id = document.Id,
                Name = PublicTypes.NameOf(document),
                PrimaryStat = document.PrimaryStat?.ToString(),
                Stats = [.. (document.Stats ?? []).Select(stat => new EnchantStatRecord(stat.Attribute, stat.Coefficient))],
            },
            document, view, origin);

    private static AugmentationRecord Augmentation(
        AugmentationRecord seed, AugmentationDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];

        return seed with
        {
            Generates = Generated(document.Generates),
            Category = document.Category,
            MaxRank = document.MaxRank,
            RandomChance = document.RandomChance,
            Description = view.PlainText(document.Description, tokens, []),
            DescriptionsByHero = Readings(document.DescriptionsByHero, view, tokens),
            Icon = Texture(document.Icon, view),
            Cooldown = document.Cooldown,
            Duration = document.Duration,
            RankRuns = Runs(document.RankRuns),
            Placeholders = Tokens(tokens, [], view),
            ScalesOff = Scaling(document.ScalesOff, view),
            Grants = References(document.Grants, view, origin),
        };
    }

    public static SigilRecord From(SigilDocument document, ICodexViewService view, string origin) =>
        (SigilRecord)Augmentation(
            new SigilRecord { Type = EntityType.Sigil, Id = document.Id, Name = PublicTypes.NameOf(document) },
            document, view, origin);

    public static AspectRecord From(AspectDocument document, ICodexViewService view, string origin) =>
        (AspectRecord)Augmentation(
            new AspectRecord { Type = EntityType.Aspect, Id = document.Id, Name = PublicTypes.NameOf(document) },
            document, view, origin);

    public static ProtectiveEnchantRecord From(
        ProtectiveEnchantDocument document, ICodexViewService view, string origin) =>
        (ProtectiveEnchantRecord)Augmentation(
            new ProtectiveEnchantRecord
            {
                Type = EntityType.ProtectiveEnchant, Id = document.Id, Name = PublicTypes.NameOf(document),
            },
            document, view, origin);

    public static WeaponPowerRecord From(WeaponPowerDocument document, ICodexViewService view, string origin)
    {
        var tokens = document.Placeholders ?? [];
        var tags = document.Schools ?? [];

        return new WeaponPowerRecord
        {
            Type = EntityType.WeaponPower,
            Id = document.Id,
            Generates = Generated(document.Generates),
            Name = PublicTypes.NameOf(document),
            Description = view.PlainText(document.Description, tokens, tags),
            Icon = Texture(document.Icon, view),
            CastTime = document.CastTime,
            ChannelTime = document.ChannelTime,
            ChannelTick = document.ChannelTick,
            Cooldown = document.Cooldown,
            CooldownDurationScalesOff = Scaling(document.CooldownDurationScalesOff, view),
            CooldownTimeRateScalesOff = Scaling(document.CooldownTimeRateScalesOff, view),
            CooldownHasteGrantedBy = References(document.CooldownHasteGrantedBy, view, origin),
            CooldownAccelerationGrantedBy = References(document.CooldownAccelerationGrantedBy, view, origin),
            Duration = document.Duration,
            Period = document.Period,
            Range = document.Range,
            Radius = document.Radius,
            CastTimeScalesWithHaste = document.CastTimeScalesWithHaste,
            Schools = Some(tags),
            Placeholders = Tokens(tokens, tags, view),
            ScalesOff = Scaling(document.ScalesOff, view),
        };
    }

    private static string? Texture(string? icon, ICodexViewService view) => icon is null ? null : view.Icon(icon);

    private static IReadOnlyList<T>? Some<T>(IReadOnlyList<T>? values) => values is { Count: > 0 } list ? list : null;

    internal static IReadOnlyList<string>? Named(IEnumerable<string> values) =>
        values.ToArray() is { Length: > 0 } named ? named : null;

    private static IReadOnlyList<string>? Scaling(IReadOnlyList<string>? attributes, ICodexViewService view) =>
        Named(view.Codex.Scaling(attributes));

    private static IReadOnlyList<string>? Lines(
        IReadOnlyList<string>? detail,
        ICodexViewService view,
        IReadOnlyList<Placeholder> tokens,
        IReadOnlyList<string> tags) =>
        detail is { Count: > 0 } lines ? [.. lines.Select(line => view.PlainText(line, tokens, tags)!)] : null;

    private static IReadOnlyDictionary<string, string>? Readings(
        IReadOnlyDictionary<string, string>? byHero, ICodexViewService view, IReadOnlyList<Placeholder> tokens) =>
        byHero is { Count: > 0 } entries
            ? entries.ToDictionary(entry => entry.Key, entry => view.PlainText(entry.Value, tokens, [])!)
            : null;

    private static AttunementTreeRecord? Tree(string? tag, ICodexViewService view, string origin)
    {
        if (AttunementTrees.Find(tag, view.Store.Settings) is not { } tree) return null;

        return new AttunementTreeRecord(tree.Tag)
        {
            UseRecipeRowUnlocks = tree.UseRecipeRowUnlocks,
            RowsUnlockedAtItemGeneration = tree.RowsUnlockedAtItemGeneration,
            RowToUpgradeRequirement = tree.RowToUpgradeRequirement,
            Nodes =
            [
                .. tree.Nodes.Select(node => new AttunementTreeNodeRecord(node.UITreeIndex, node.AugmentationRowIndex)
                {
                    ParentNodes = node.ParentNodes,
                    ChildrenNodes = node.ChildrenNodes,
                    AugmentationCategories =
                    [
                        .. node.AugmentationCategories.Select(category => new AugmentationCategoryRecord(category)
                        {
                            Name = AttunementTrees.CategoryName(category, view.Store),
                            Augmentations =
                            [
                                .. AttunementTrees.InCategory(category, view.Store)
                                    .Select(augmentation => Referenced(EntityTypes.TypeOf(augmentation), augmentation.Id, view, origin)),
                            ],
                        }),
                    ],
                    RandomizedFromAugmentationCategories = node.RandomizedFromAugmentationCategories,
                    MinDungeonRatingAtGenerationTime = node.MinDungeonRatingAtGenerationTime,
                    EnableAtItemLevel = node.EnableAtItemLevel,
                    MaxRankOverride = node.MaxRankOverride,
                }),
            ],
            UpgradeCosts =
            [
                .. view.Store.Settings.AttunementNodeUpgradeCosts.Select(cost => new AttunementNodeUpgradeCostRecord(
                    cost.Rank, cost.Cost, Referenced(EntityType.Currency, cost.Currency, view, origin))),
            ],
        };
    }

    internal static RecordReference? Reference(EntityReference? source, ICodexViewService view, string origin) =>
        source is null ? null : Referenced(source.Type, source.Id, view, origin);

    internal static RecordReference Referenced(
        EntityType type, int id, ICodexViewService view, string origin) =>
        new(
            type,
            id,
            view.Name(type, id),
            $"{origin}/api/{EntityTypes.Slug(type)}/{id.ToString(CultureInfo.InvariantCulture)}");

    private static IReadOnlyList<RecordReference>? References(
        IReadOnlyList<EntityReference>? sources, ICodexViewService view, string origin) =>
        sources?.Select(source => Referenced(source.Type, source.Id, view, origin)).ToArray()
            is { Length: > 0 } named
            ? named
            : null;

    private static IReadOnlyList<TargetCountFalloffRecord>? Falloff(
        IReadOnlyList<TargetCountFalloff>? entries, ICodexViewService view, string origin) =>
        entries?.Select(entry => new TargetCountFalloffRecord(entry.Threshold)
            {
                Effect = Reference(entry.Effect, view, origin),
            })
            .ToArray() is { Length: > 0 } scaled
            ? scaled
            : null;

    private static IReadOnlyList<CreatureAbilityRecord>? Wielded(
        IReadOnlyList<CreatureAbility>? abilities, ICodexViewService view, string origin) =>
        abilities?.Select(slot => new CreatureAbilityRecord(
                Referenced(slot.Ability.Type, slot.Ability.Id, view, origin), slot.Phase, slot.Shown)
            {
                EnabledByDefault = slot.EnabledByDefault,
                InitialCooldown = slot.InitialCooldown,
                Cooldown = slot.Cooldown,
                InterruptCooldown = slot.InterruptCooldown,
                CCCooldown = slot.CCCooldown,
                PostAbilityPadding = slot.PostAbilityPadding,
            })
            .ToArray() is { Length: > 0 } wielded
            ? wielded
            : null;

    private static IReadOnlyList<RecordReference>? Dwelling(
        DungeonDocument dungeon, ICodexViewService view, string origin) =>
        view.Store.Entities
            .OfType<CreatureDocument>()
            .Where(creature => creature.FoundIn?.Any(level => level.Id == dungeon.Id) is true)
            .Select(creature => Referenced(EntityType.Creature, creature.Id, view, origin))
            .ToArray() is { Length: > 0 } found
            ? found
            : null;

    private static IReadOnlyList<ItemSourceRecord>? Sourced(
        IReadOnlyList<ItemSource>? sources, ICodexViewService view, string origin) =>
        sources?.Select(source => new ItemSourceRecord(source.Kind, source.Name)
            {
                Dungeon = Reference(source.Dungeon, view, origin),
            })
            .ToArray() is { Length: > 0 } dropped
            ? dropped
            : null;

    private static IReadOnlyList<LegendaryStatRecord>? Rolls(
        IReadOnlyList<EntityReference>? sources, ICodexViewService view) =>
        sources
            ?.SelectMany(source => view.Store.All(source.Type, source.Id))
            .OfType<LegendaryStatDocument>()
            .Select(roll => From(roll, view, string.Empty))
            .ToArray() is { Length: > 0 } rolled
            ? rolled
            : null;

    private static IReadOnlyList<StatSlotRecord>? Slots(ItemDocument document, ICodexViewService view) =>
        document.Stats is { Count: > 0 } slots
            ? [.. slots.Select(slot => new StatSlotRecord(view.Store.StatName(slot.Stat), slot.Weight, slot.Kind))]
            : null;

    private static IReadOnlyList<string>? RarityNames(IReadOnlyList<Rarity>? rarities, ICodexViewService view) =>
        rarities?.Select(rarity => view.RarityName(rarity)).OfType<string>().ToArray() is { Length: > 0 } named
            ? named
            : null;

    private static IReadOnlyDictionary<string, string>? IconsByRarity(
        ItemDocument document, ICodexViewService view)
    {
        if (document.IconByRarity is not { Count: > 0 } texture) return null;

        var printed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (rarity, icon) in texture)
        {
            var known = view.Store.Settings.Rarities.FirstOrDefault(entry =>
                string.Equals(entry.Name, rarity, StringComparison.OrdinalIgnoreCase));

            if (known is not null) printed[known.DisplayName] = view.Icon(icon);
        }

        return printed.Count == 0 ? null : printed;
    }

    private static GemOverflowRecord? Overflow(EntityDocument document, ICodexViewService view)
    {
        if (view.Stone(document) is not { OverflowDescription: { } description } gem) return null;

        var tokens = gem.OverflowPlaceholders ?? [];

        return new GemOverflowRecord(view.PlainText(description, tokens, [])!)
        {
            Placeholders = Tokens(tokens, [], view),
            Icon = Texture(gem.Icon, view),
        };
    }

    private static IReadOnlyList<RankRunRecord>? Runs(IReadOnlyList<RankRun>? runs) =>
        runs is { Count: > 0 } ranked
            ? [.. ranked.Select(run => new RankRunRecord(run.Literal, run.Printed)
            {
                Values = run.Values,
                Transform = run.Transform,
                Ticks = run.Ticks,
                Disagrees = run.Disagrees,
            })]
            : null;

    private static IReadOnlyList<ResourceGainRecord>? Generated(IReadOnlyList<ResourceGain>? gains) =>
        gains is not { Count: > 0 }
            ? null
            : [.. gains.Select(gain => new ResourceGainRecord(gain.Resource)
            {
                ResourceName = gain.ResourceName,
                Amount = gain.Amount,
                AmountOnCritical = gain.AmountOnCritical,
                ShareOfMaximum = gain.ShareOfMaximum,
                Per = gain.Per,
                Chance = gain.Chance,
                RowGroup = gain.RowGroup,
                Source = gain.Source,
                SourceOnCritical = gain.SourceOnCritical,
                SourceKind = gain.SourceKind,
            })];

    internal static IReadOnlyList<PlaceholderRecord>? Tokens(
        IReadOnlyList<Placeholder> tokens, IReadOnlyList<string> tags, ICodexViewService view) =>
        tokens.Select((token, at) => Token(token, view, at + 1, tags)).ToArray() is { Length: > 0 } spent
            ? spent
            : null;

    private static PlaceholderRecord Token(
        Placeholder token, ICodexViewService view, int ordinal, IReadOnlyList<string>? tags) => new()
    {
        Ordinal = ordinal,
        Reading = view.Reading(token, tags),
        Attribute = view.Store.StatDisplay(token.Attribute)?.DisplayName is { Length: > 0 } printed ? printed : null,
        Coefficient = token.Coefficient,
        Spread = token.Spread,
        Value = token.Value,
        Text = token.Text,
        Percent = token.Percent,
        Format = token.Format,
        Unit = token.Unit,
        ShowDamageType = token.ShowDamageType,
        ScalesOff = Named(view.Codex.Scaling(token.ScalesOff)),
    };
}
