using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public delegate string? IconWriter(string iconObjectPath, string? rarity);

public sealed class EntityResolver
{
    private const string MappingKeyField = "GrantedAbilityMappingKey";
    private const string RuleSetField = "RuleSet";
    private const string RuleSetPrefix = "DynamicHeroItemRuleSet.";
    private const string VisualKeyField = "VisualCollectionMappingKey";
    private const string SlotOrdinalField = "HeroItemType";
    private const string StaminaStat = "Stamina";
    private const string DungeonLevelType = "Dungeon";
    private const string AdventureLevelType = "Adventure";
    private const string HoveredIconField = "UIIconHovered";
    private const string EffectTable = "GameplayEffect";
    private const string CostEffectClass = "/Script/fellowship.CRCostEffect";
    private const string AbilityTypePrefix = "AbilityType.";
    private const string TalentTagPrefix = "Talents.";
    private const string TagSuffix = "Tag";
    private const int MinimumGatePrefix = 8;
    private const string GrantedTagsField = "GrantedGameplayTags";
    private const string GrantedEffectsField = "GrantedGameplayEffects";
    private const string AbilityTagsField = "AbilityTags";
    private const string CooldownTagsField = "CooldownTags";
    private const string DynamicDefinitionClass = DynamicItemRuleSets.DefinitionClass;
    private const string SpecificDungeonsField = "DropsInSpecificDungeons";
    private const string CreatureTable = "CRAICharacter";
    private const string ItemTable = "ItemID";
    private const string AbilityTable = "GameplayAbility";
    private const string CharacterIdField = "CharacterID";
    private const string CharacterTagsField = "CharacterTags";
    private const string CountsTowardScoreField = "CountTowardsDungeonScore";
    private const string UnlockCriteriaField = "UnlockCriteria";

    private readonly AbstractFileProvider _provider;
    private readonly TypeMappings _mappings;

    private readonly IReadOnlySet<string> _multiplyingOps;
    private readonly FAssetData[] _assets;
    private readonly ConstantsTable _constants;
    private readonly EffectCalculationPresets _presets;
    private readonly CooldownScaling _cooldowns;
    private AugmentationIndex? _augmentations;

    private readonly Dictionary<string, bool> _spreadSources = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _contestedBands = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _unbindableSpreadRows = new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<TagRecord> _tags;
    private readonly AugmentationIds _augmentationIds;
    private readonly RarityNames _rarities;
    private readonly Dictionary<string, FAssetData> _byPrimaryAssetName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FAssetData> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FAssetData> _petsByTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FAssetData> _mountsByTag = new(StringComparer.OrdinalIgnoreCase);

    private const string MountClass = "CRMountData";
    private const string TextureClass = "Texture2D";
    private const string EffectTagsField = "InheritableGameplayEffectTags";
    private const string CombinedTagsField = "CombinedTags";
    private const string MountIdField = "MountID";
    private const string MountRarityField = "Rarity";
    private readonly HashSet<string> _effectNames = new(StringComparer.OrdinalIgnoreCase);

    public AbilityPresentationIndex Presentation { get; }
    public EffectIconSources EffectIcons { get; }
    public EffectNameSources EffectNames { get; }
    public GrantedAbilityMappings Mappings { get; }
    public HeroNames Heroes { get; }
    public ItemSlots Slots { get; }
    public StatCalculation StatCalculation { get; }

    public ModifierRecipes ModifierRecipes { get; }
    public ItemSlotNames SlotNames { get; }
    public SlotIcons SlotGlyphs { get; }

    public TemperCeilings Tempers { get; }
    public DropItemLevels Drops { get; }
    public DungeonNames Dungeons { get; }
    public ModifierInfo Modifiers { get; }
    public VendorInventories Vendors { get; }
    public ItemSources Sources { get; }

    public ChallengeRewards Challenges { get; }
    public AbilityOwners Owners { get; }
    public ResourcePools Pools { get; }
    public ResourceNames ResourceNames { get; }
    public ResourceGains Gains { get; }

    public TargetCountFalloffs Falloffs { get; }
    public ArmorTypes Armor { get; }
    public HeroItemTable HeroItems { get; }
    public VisualCollections Collections { get; }
    public DynamicItemVisuals DynamicVisuals { get; }
    public ItemTableNames TableNames { get; }
    public ProgressionItems Progression { get; }
    public CustomizationItems Customizations { get; }
    public LocalizedText Localized { get; }
    public MountLore Lore { get; }
    public HeroAttributes HeroAttributes { get; }
    public AttunementTrees AttunementTrees { get; }
    public DynamicItemRuleSets RuleSets { get; }
    public RarityDropChances RarityDrops { get; }
    public DynamicAbilityRanks AbilityRanks { get; }
    public TalentTrees Talents { get; }
    public GemDefinitions Gems { get; }
    public AugmentationRows AugmentationRows { get; }
    public StatEnchants Enchants { get; }
    public DynamicTextRows DynamicText { get; }
    public Settings Settings { get; }
    public AbilityCategories Categories { get; }
    public TypeResolver Types { get; }
    public CreatureStats CreatureStats { get; }
    public CreatureLocations Locations { get; }
    public DungeonLayouts Layouts { get; }
    public DungeonDifficulties DungeonDifficulties { get; }
    public Leagues Leagues { get; }

    public int NamespaceSupplied { get; private set; }

    public int IconsInherited { get; private set; }

    public int NamesInherited { get; private set; }

    public int NamesContested { get; private set; }

    public int IconsContested { get; private set; }

    public int PresetCount => _presets.Count;

    public int SpreadOverrides => _spreadSources.Values.Count(fromRow => fromRow);

    public int SpreadPresetDefaults => _spreadSources.Values.Count(fromRow => !fromRow);

    public IReadOnlyCollection<string> SpreadContested => _contestedBands;

    public IReadOnlyCollection<string> SpreadRowsUnbindable => _unbindableSpreadRows;

    public EntityResolver(AbstractFileProvider provider)
    {
        if (provider.MappingsContainer?.MappingsForGame is not { } mappings)
        {
            throw new InvalidOperationException(
                "No mappings are loaded, so every cooked package reads back as nothing and every table looks " +
                "absent. Set FELLOWSHIP_USMAP to a .usmap for this build, or write " +
                $"mappings/{ExtractorOptions.MappingsPattern} with " +
                "'dotnet run --project src/FellowshipExtractor -- usmap'.");
        }

        _provider = provider;
        _mappings = mappings;
        _assets = [.. AssetRegistry.ReadAssets(provider)];
        var tags = CsvTables.ReadTags(provider).ToArray();
        _tags = CsvTables.Current(tags);
        Superseded = tags.Length - _tags.Count;
        _constants = new ConstantsTable(provider);
        _presets = new EffectCalculationPresets(provider, _assets);
        _cooldowns = new CooldownScaling(provider, _assets, _constants);
        _multiplyingOps = Reductions.MultiplyingOps(mappings);

        IndexAssets();
        IndexPets();
        IndexMounts();

        foreach (var tag in _tags)
        {
            if (tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) _effectNames.Add(tag.Name);
        }

        _augmentationIds = new AugmentationIds(_tags);
        var rarities = _rarities = new RarityNames(provider, _assets);

        Presentation = new AbilityPresentationIndex(provider, _assets);
        Mappings = new GrantedAbilityMappings(provider, _assets);
        Slots = new ItemSlots(provider, _assets);
        SlotNames = new ItemSlotNames(provider, _assets);
        SlotGlyphs = new SlotIcons(provider, SlotNames);
        Tempers = new TemperCeilings(provider, rarities);
        Drops = new DropItemLevels(provider, _assets);
        Armor = new ArmorTypes(provider, _assets);
        TableNames = new ItemTableNames(provider, _assets, rarities);
        Progression = new ProgressionItems(provider, _assets, rarities);
        Localized = new LocalizedText(provider);
        Lore = new MountLore(Localized);
        RarityDrops = new RarityDropChances(provider, rarities);
        RuleSets = new DynamicItemRuleSets(provider, _assets, rarities, RarityDrops);
        Gems = new GemDefinitions(provider, _assets, _constants, _augmentationIds, rarities);
        ModifierRecipes = new ModifierRecipes(provider, _assets);
        StatCalculation = new StatCalculation(provider, _assets, SlotNames, Armor, RuleSets, ModifierRecipes);

        AugmentationRows = new AugmentationRows(
            provider, _assets, _constants, _augmentationIds,
            new AugmentationCategoryNames(provider, _assets, StatCalculation.SlotDefinitions, ModifierRecipes.Types));

        AttunementTrees = new AttunementTrees(provider, _assets, _tags, AugmentationRows.MaxRank);

        AbilityRanks = new DynamicAbilityRanks(provider, _assets, _tags, _augmentationIds, SlotNames);

        Collections = new VisualCollections(provider, _assets);
        HeroItems = new HeroItemTable(provider, _assets, Armor, SlotNames, Collections, rarities);
        Heroes = new HeroNames(provider, _assets, Armor, HeroItems.PrimaryStatByHero);
        Enchants = new StatEnchants(provider, AugmentationRows, _constants, Heroes);
        HeroAttributes = new HeroAttributes(provider, Heroes);
        DynamicText = new DynamicTextRows(provider, _assets, _constants, Heroes);

        DynamicVisuals = new DynamicItemVisuals(provider, _assets, Collections, rarities, RarityDrops);

        Talents = new TalentTrees(provider, _assets, _tags, Heroes);

        Dungeons = new DungeonNames(provider, _assets);
        Customizations = new CustomizationItems(provider, _assets, rarities, Heroes, Dungeons);
        Modifiers = new ModifierInfo(provider, _assets);
        Vendors = new VendorInventories(provider, _assets);
        Challenges = new ChallengeRewards(provider, _assets);
        Sources = new ItemSources(provider, _assets, Dungeons, Vendors, Challenges, HeroItems, Drops, Heroes);

        Leagues = new Leagues(provider, _assets);
        CreatureStats = new CreatureStats(provider, Heroes, Leagues.Listed, Characters());

        string[] creatures =
        [
            .. _tags.Where(tag => tag.Table.Equals(CreatureTable, StringComparison.OrdinalIgnoreCase))
                .Select(tag => tag.Name),
        ];

        Locations = new CreatureLocations(provider, Dungeons, creatures);
        Layouts = new DungeonLayouts(provider, Dungeons, creatures);
        DungeonDifficulties = new DungeonDifficulties(provider, _assets, Leagues.Matched);

        Categories = new AbilityCategories(provider);

        Settings = new Settings(
            provider, _assets, Heroes, rarities, Categories, StatCalculation, Gems.Types,
            SlotGlyphs, new RarityOrnaments(provider, rarities), new TalentTierPlates(provider),
            new RarityColorRows(provider, rarities),
            new SectionHeadingRows(provider), Tempers, Drops, new GlyphWording(provider, _constants),
            CreatureStats, Leagues, new ItemSettings(provider, SlotNames, Gems, rarities), Enchants, AttunementTrees,
            ModifierRecipes, new TooltipWidgetText(provider, _assets, SlotNames), new AbilityTooltipTexts(provider, _assets));

        EffectIcons = new EffectIconSources(
            _tags, _effectNames, Presentation, tag => LoadDefaultObject(FindAsset(tag.Name)));

        EffectNames = new EffectNameSources(
            _tags, _effectNames, tag => LoadDefaultObject(FindAsset(tag.Name)), PrintedName, LoadClassDefault);

        Types = new TypeResolver(
            _tags, Talents, Gems, AugmentationRows, Mappings, Dungeons, Progression, Customizations, TableNames, Heroes,
            _mountsByTag.Keys);

        Owners = new AbilityOwners(provider, _assets, _tags, Heroes, _constants);
        Pools = new ResourcePools(provider, Heroes, _constants);
        ResourceNames = new ResourceNames(Pools, Heroes);

        Gains = new ResourceGains(
            provider, mappings, _constants, Pools, Owners, Heroes, ResourceNames, _tags,
            name => LoadDefaultObject(FindAsset(name)));

        Falloffs = new TargetCountFalloffs(provider, mappings, _constants, _tags, name => LoadDefaultObject(FindAsset(name)));
    }

    private string? PrintedName(TagRecord tag, UObject cdo)
    {
        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            if (Talents.ForAugmentation(tag.Name)?.DisplayName is { } talent) return talent;
            if (Gems.ForAugmentation(tag.Name)?.DisplayName is { } node) return node;
            if (Enchants.ForAugmentation(tag.NetId)?.Name is { } enchant) return enchant;
            if (AugmentationRows.ForAugmentation(tag.NetId)?.DisplayName is { } augmentation) return augmentation;
        }

        return Display.From(Presentation.Find(tag.Name)?.Row, cdo).Name;
    }

    private static string ShortRuleSet(string tagName) =>
        tagName.StartsWith(RuleSetPrefix, StringComparison.OrdinalIgnoreCase)
            ? tagName[RuleSetPrefix.Length..]
            : tagName;

    public int ConstantCount => _constants.Count;

    public IReadOnlyList<TagRecord> Tags => _tags;

    public int Superseded { get; }

    public TagRecord? FindTag(string table, string id)
    {
        var wantsId = int.TryParse(id, out var netId);

        foreach (var tag in _tags)
        {
            if (!tag.Table.Equals(table, StringComparison.OrdinalIgnoreCase)) continue;
            if (wantsId ? tag.NetId == netId : tag.Name.Equals(id, StringComparison.OrdinalIgnoreCase)) return tag;
        }

        return null;
    }

    public IReadOnlyList<Entity> Resolve(TagRecord tag, IconWriter? icons)
    {
        var asset = FindAsset(tag.Name);
        var cdo = LoadDefaultObject(asset);
        var presentation = Presentation.Find(tag.Name);
        var text = (IPropertyHolder?)presentation?.Row ?? EffectUiData.Find(cdo);
        var display = Display.From(text, cdo);

        var variants = cdo is null ? [] : HeroVariants.Collect(cdo, Heroes);

        var restricted = variants.Count > 0;

        var body = Body(cdo, asset?.AssetName.Text ?? tag.Name, display, text, icons);
        if (cdo is not null) body = FillFrom(body, GrantedAbilities(cdo, icons));

        var (granted, grantedPath) = ResolveGranted(cdo, variants, restricted, icons);

        var ruleSet = cdo is null ? null : PropertyWalk.GetString(cdo, RuleSetField);

        var entity = body with
        {
            Table = tag.Table,
            NetId = tag.NetId,
            Name = tag.Name,
            Slot = IsDynamicDefinition(asset) && cdo is not null
                ? SlotNames.ByOrdinal(PropertyWalk.AsOrdinal(PropertyWalk.Get(cdo, SlotOrdinalField)?.GenericValue) ?? 0)?.Shipped
                : null,
            GemSlot = RuleSets.HasGemSocket(ruleSet) ? true : null,
            AttunementTree = cdo is null ? null : AttunementTrees.Named(PropertyWalk.GetString(cdo, AttunementTrees.ItemField)),
            RuleSet = ruleSet is null ? null : ShortRuleSet(ruleSet),
            Rarities = cdo is null ? null : Empty(DynamicVisuals.Rarities(cdo)),
            Stats = cdo is null ? null : Empty(Slots.For(cdo, StatCalculation)),
            Grants = granted,
            GrantsId = _augmentationIds.For(grantedPath),
            LegendaryStats = cdo is null ? null : Empty(LegendaryStats(cdo, icons)),
            LegendaryStatIds = cdo is null ? null : Empty(LegendaryStatIds(cdo)),
            UntaggedLegendaryStats = cdo is null ? null : Empty(UntaggedLegendaryStats(cdo, icons)),
            Variants = Empty(variants),
        };

        entity = WithTableText(entity, tag.Name, icons);
        entity = WithAbilityRank(entity, cdo, icons);

        if (HeroItems.Find(tag.Name) is { } fixedItem) entity = WithFixedItem(entity, fixedItem, icons);

        entity = WithProvenance(entity, tag, asset, cdo);

        if ((tag.Table.Equals(AbilityTable, StringComparison.OrdinalIgnoreCase) ||
             tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) &&
            Spellings(tag.Name).All(spelling => FindAsset(spelling) is null))
        {
            entity = entity with { Uncooked = true };
        }

        entity = WithOwners(entity, tag);

        entity = WithSpentResource(entity, display, tag, cdo);
        entity = WithGeneratedResources(entity);
        entity = WithSuppliedDuration(entity, cdo, tag);

        if (cdo is not null && tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase))
        {
            entity = entity with { EffectTags = Empty(EffectTags(cdo)) };
        }

        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            if (DynamicText.ForTag(tag.Name) is { } sentences)
            {
                entity = entity with { DescriptionsByHero = sentences };
            }

            entity = entity with { Granted = Empty(Granting(tag.Name)) };

            if (Talents.ForAugmentation(tag.Name) is { } talent) entity = WithTalent(entity, talent, icons);
            if (Gems.ForAugmentation(tag.Name) is { } node) entity = WithGemNode(entity, node, icons);
            if (Enchants.ForAugmentation(tag.NetId) is { } enchant)
            {
                entity = entity with
                {
                    DisplayName = entity.DisplayName ?? enchant.Name,
                    NameByPrimaryStat = enchant.NameByPrimaryStat,
                    Placeholders = Overlaid(entity.Placeholders, enchant.Placeholders),
                };
            }

            if (AugmentationRows.ForAugmentation(tag.NetId) is { } grantingAugmentation)
            {
                entity = WithGrantorText(entity, grantingAugmentation.DisplayName, grantingAugmentation.Description, grantingAugmentation.IconPath, icons);
            }
        }

        if (tag.Table.Equals(CreatureTable, StringComparison.OrdinalIgnoreCase))
        {
            entity = WithCreature(entity, tag, cdo);
        }

        if (tag.Table.Equals(TypeResolver.LevelTable, StringComparison.OrdinalIgnoreCase) &&
            Dungeons.Find(tag.Name) is { } dungeon)
        {
            entity = WithDungeon(entity, dungeon, icons);
        }

        if (tag.Table.Equals(ModifierInfo.Table, StringComparison.OrdinalIgnoreCase) &&
            Modifiers.ForTag(tag.Name) is { } modifier)
        {
            entity = WithModifier(entity, modifier, icons);
        }

        if (tag.Table.Equals(HeroNames.Table, StringComparison.OrdinalIgnoreCase) &&
            Heroes.ForTag(tag.Name) is { } hero)
        {
            entity = WithHero(entity, hero, icons);
        }

        entity = entity with { Type = Types.Of(tag, entity, asset?.PackageName.Text) };

        if (Owners.DisabledInSet(tag.Name) ||
            (presentation is not null &&
             PropertyWalk.Get(presentation.Row, AbilityOwners.EnabledInGameField)?.GenericValue is false) ||
            (IsDynamicDefinition(asset) && cdo is not null &&
             PropertyWalk.Get(cdo, AbilityOwners.EnabledInGameField)?.GenericValue is false))
        {
            entity = entity with { EnabledInGame = false };
        }

        if (entity.Type is EntityType.CompanionPet &&
            LoadDefaultObject(_petsByTag.GetValueOrDefault(tag.Name)) is { } pet)
        {
            entity = entity with { Acquire = PropertyWalk.GetText(pet, UnlockCriteriaField) };
        }

        if (entity.Type is EntityType.Mount)
        {
            entity = entity with { Lore = Lore.For(tag.Name) };

            if (LoadDefaultObject(_mountsByTag.GetValueOrDefault(tag.Name)) is { } mount)
            {
                var tier = _rarities.Tier(PropertyWalk.Get(mount, MountRarityField)?.GenericValue);

                entity = entity with
                {
                    Acquire = PropertyWalk.GetText(mount, UnlockCriteriaField),
                    Rarity = _rarities.Label(tier),
                    RarityTier = tier,
                };
            }
        }

        if (entity.DisplayName is null)
        {
            if (EffectNames.For(tag.Name) is { } named)
            {
                entity = entity with
                {
                    DisplayName = named.DisplayName,
                    NameInheritedFrom = named.Origin,
                    NameByPrimaryStat = named.Origin.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase)
                        ? Enchants.ForAugmentation(named.Origin.NetId)?.NameByPrimaryStat
                        : null,
                };
                NamesInherited++;
            }
            else if (EffectNames.Disagrees(tag.Name))
            {
                NamesContested++;
            }
        }

        if (entity is { Icon: null, DisplayName: not null })
        {
            if (EffectIcons.For(tag.Name) is { } source && IconName(source.IconPath, icons) is { } file)
            {
                entity = entity with { Icon = file, IconInheritedFrom = source.Origin };
                IconsInherited++;
            }
            else if (EffectIcons.Disagrees(tag.Name))
            {
                IconsContested++;
            }
        }

        if (entity.NameByPrimaryStat is { } byStat)
        {
            return
            [
                .. byStat.Select(named => entity with
                {
                    DisplayName = named.Value,
                    PrimaryStat = named.Key,
                    NameByPrimaryStat = null,
                }),
            ];
        }

        if (tag.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase) &&
            Talents.InSlot(tag.NetId) is { Count: > 0 } occupants)
        {
            return TalentTrees.Expand(entity, occupants, icons);
        }

        return cdo is not null && ItemNames.Read(cdo) is { } naming
            ? ItemVariants.Expand(entity, naming, Heroes, DynamicTextures(cdo, icons), WearsArmor)
            : [entity];
    }

    private bool WearsArmor(Entity item) =>
        item.Slot is { } slot &&
        (HeroItems.ArmoredSlots.Contains(slot) ||
         (StatCalculation.SlotWeights.TryGetValue(slot, out var weight) && weight.ArmorWeight > 0 &&
          item.Stats?.Any(stat => stat.Stat.Equals(StaminaStat, StringComparison.OrdinalIgnoreCase)) is true));

    private HeroTextures DynamicTextures(UObject cdo, IconWriter? icons)
    {
        var mappingKey = PropertyWalk.GetString(cdo, VisualKeyField);
        var slot = SlotNames.ByOrdinal(PropertyWalk.AsOrdinal(PropertyWalk.Get(cdo, SlotOrdinalField)?.GenericValue) ?? 0);

        return codename =>
        [
            .. DynamicVisuals.Icons(mappingKey, codename, slot?.Type)
                .Select(entry => (entry.Rarity, Image: IconName(entry.IconPath, icons, entry.Rarity)))
                .Where(entry => entry.Image is not null)
                .Select(entry => new RarityImage(entry.Rarity, entry.Image!)),
        ];
    }

    private Entity WithAbilityRank(Entity entity, UObject? cdo, IconWriter? icons)
    {
        if (entity.NetId is not { } netId) return entity;
        if (AbilityRanks.For(netId) is not { } row) return entity;

        return entity with
        {
            AbilityMaxRank = row.MaxRank,
            PoolSlots = row.Slots,
            AbilityRank = row.NetId,
            IconHovered = cdo is null ? null : IconName(PropertyWalk.GetString(cdo, HoveredIconField), icons),
        };
    }

    private Entity WithTableText(Entity entity, string tagName, IconWriter? icons)
    {
        if (AugmentationRows.ForTag(tagName) is { } augmentation)
        {
            var enchant = augmentation.Augmentation is { } granting ? Enchants.ForAugmentation(granting) : null;

            return entity with
            {
                DisplayName = entity.DisplayName ?? enchant?.Name ?? augmentation.DisplayName,
                Description = entity.Description ?? enchant?.Description ?? augmentation.Description,
                Placeholders = entity.Placeholders ?? enchant?.Placeholders,
                AugmentationCategoryTag = augmentation.CategoryTag,
                AugmentationCategory = augmentation.Category,
                MaxRank = augmentation.MaxRank,
                RandomChance = augmentation.RandomChance,
                Augmentation = augmentation.Augmentation,
                RankRuns = augmentation.Runs,
                Icon = entity.Icon ?? IconName(augmentation.IconPath, icons),
                NameByPrimaryStat = enchant?.NameByPrimaryStat,
                EnchantStats = enchant?.Stats,
            };
        }

        if (Gems.ForType(tagName) is { } gem)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? gem.DisplayName,
                GemType = gem.DisplayName,
                GemAttribute = gem.Attribute,
                GemOverflow = WithOverflow(gem.Overflow, icons),
            };
        }

        if (Gems.ForSize(tagName) is { } size)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? size.DisplayName,
                GemType = size.TypeName,
                GemTier = size.Tier,
                GemPower = size.Power,
                Rarity = size.Rarity,
                RarityTier = size.RarityTier,
                Icon = entity.Icon ?? IconName(size.IconPath, icons),
            };
        }

        if ((TableNames.Find(tagName) ?? Progression.Find(tagName)) is { } row)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? row.DisplayName,
                Description = entity.Description ?? row.Description,
                Rarity = entity.Rarity ?? row.Rarity,
                RarityTier = entity.RarityTier ?? row.RarityTier,
                Icon = entity.Icon ?? IconName(row.IconPath, icons),
                EnabledInGame = row.EnabledInGame ? entity.EnabledInGame : false,
            };
        }

        if (Customizations.Find(tagName) is { } customization)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? customization.DisplayName,
                Description = entity.Description ?? customization.Description,
                Acquire = customization.Acquire,
                TitleText = customization.TitleText,
                Rarity = entity.Rarity ?? customization.Rarity,
                RarityTier = entity.RarityTier ?? customization.RarityTier,
                Icon = entity.Icon ?? IconName(customization.IconPath, icons),
            };
        }

        return entity;
    }

    private static Entity WithGemNode(Entity entity, GemNode node, IconWriter? icons) => entity with
    {
        DisplayName = node.DisplayName ?? entity.DisplayName,
        Description = node.Description ?? entity.Description,
        Placeholders = node.Placeholders ?? entity.Placeholders,
        GemType = node.TypeName,
        GemNode = node.Node,
        RequiredGemPower = node.RequiredPower,
        Replaces = node.Replaces,
        Icon = IconName(node.IconPath, icons) ?? entity.Icon,
    };

    private static GemOverflow? WithOverflow(GemOverflow? overflow, IconWriter? icons) =>
        overflow is null ? null : overflow with { Icon = IconName(overflow.Icon, icons) };

    private static string? IconName(string? iconPath, IconWriter? icons, string? rarity = null) =>
        iconPath is null ? null : icons?.Invoke(iconPath, rarity);

    private IReadOnlyDictionary<string, string>? TextureByRarity(string? iconPath, IconWriter? icons)
    {
        if (iconPath is null || icons is null || !Collections.Renders.Contains(iconPath)) return null;

        var spread = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rarity in Settings.Rarities)
        {
            if (icons(iconPath, rarity.Name) is { } image) spread[rarity.Name] = image;
        }

        return spread.Count == 0 ? null : spread;
    }

    private Entity WithOwners(Entity entity, TagRecord tag)
    {
        var claimed = Owners.HeroesOf(tag.Name).Select(hero => Heroes.Shipped(hero) ?? hero).ToArray();
        var owner = Owned(Owners.CreatureOf(tag.Name), CreatureTable)
            ?? Owned(Owners.OutsiderOf(tag.Name), ItemTable);

        var applier = tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)
            ? Owners.ApplierOf(tag.Name)
            : null;

        var appliedBy = Owned(applier, AbilityTable);

        if (claimed.Length == 0 && owner is null && appliedBy is null) return entity;

        return entity with
        {
            OwningHeroes = Empty(claimed),
            Owner = owner,
            AppliedBy = appliedBy,
            ApplierTags = appliedBy is null ? null : Empty(Owners.AbilityTagsOf(applier!)),
        };
    }

    public int SpentResourcesResolved { get; private set; }

    private readonly HashSet<UObject> _pooledCosts = [];

    private Func<string, double?> CapturedBy(string? assetName) => attribute =>
    {
        if (assetName is null) return null;

        var values = Owners.HeroesOf(assetName)
            .Select(hero => Pools.Captured(Heroes.Shipped(hero) ?? hero, attribute))
            .Distinct()
            .ToArray();

        return values is [{ } value] ? value : null;
    };

    private Func<string, double?> SpentFrom(string? assetName)
    {
        var captured = CapturedBy(assetName);

        return attribute => Pools.Channels.Contains(attribute) ? 0 : captured(attribute);
    }

    private Entity WithSpentResource(Entity entity, Display display, TagRecord tag, UObject? cdo)
    {
        if (entity.Cost is not { } fraction || fraction == 0) return entity;
        if (cdo is not null && _pooledCosts.Contains(cdo)) return entity;
        if (!display.CostIsFraction && !Fractional(tag)) return entity;

        var pools = (entity.OwningHeroes ?? [])
            .Select(Pools.Of)
            .Where(pool => pool is not null)
            .Distinct()
            .ToArray();

        if (pools is not [{ } pool])
        {
            throw new InvalidOperationException(
                $"{entity.Name} spends {fraction} of the resource pool its presentation row prints as a " +
                $"percentage, and {(pools.Length == 0 ? "no hero it belongs to fills one" : "its heroes fill pools of different sizes")}, " +
                "so the amount it actually costs is unreadable and the fraction would export as the amount.");
        }

        SpentResourcesResolved++;

        return entity with { Cost = fraction * pool };
    }

    private bool Fractional(TagRecord tag)
    {
        if (Owners.ApplierOf(tag.Name) is not { Length: > 0 } applier) return false;

        return Display.From(Presentation.Find(applier)?.Row, null).CostIsFraction;
    }

    private Entity WithGeneratedResources(Entity entity)
    {
        if (entity.Generates is not { Count: > 0 } gains) return entity;

        return entity with { Generates = Empty(Gains.Joined(gains, entity.OwningHeroes)) };
    }

    public int SetByCallerDurationsScanned { get; private set; }

    private readonly List<string> _suppliedDurations = [];

    public IReadOnlyCollection<string> SetByCallerDurationsRecovered => _suppliedDurations;

    private Entity WithSuppliedDuration(Entity entity, UObject? cdo, TagRecord tag)
    {
        if (entity.Duration is not null) return entity;
        if (!tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) return entity;
        if (SetByCallerDurations.DeclaredTag(cdo, _mappings) is not { } supplied) return entity;

        SetByCallerDurationsScanned++;

        var applier = Owners.ApplierOf(tag.Name);
        if (applier is null or AbilityOwners.Contested) return entity;
        if (LoadDefaultObject(FindAsset(applier)) is not { } ability) return entity;

        var recovered = SetByCallerDurations.From(ability, supplied, _constants);
        if (recovered.Duration is not { } duration) return entity;

        _suppliedDurations.Add(
            $"{tag.Name} from {applier} assigning {supplied}: {recovered.Derivation}");
        DurationsFromSetByCaller++;

        return entity with { Duration = duration, DurationGates = recovered.Gates.Count == 0 ? entity.DurationGates : recovered.Gates };
    }

    private Entity WithCreature(Entity entity, TagRecord tag, UObject? cdo)
    {
        var attributes = cdo is null
            ? null
            : PropertyWalk.GetString(cdo, CharacterIdField) is { } characterId
                ? CreatureStats.For(characterId)
                : null;

        var slots = Owners.SlotsOf(tag.Name);

        var found = Locations.For(tag.Name)
            .Select(level => Dungeons.Find(level) is { } dungeon && FindTag(TypeResolver.LevelTable, level) is { } row
                ? new IconSource(TypeResolver.LevelTable, row.NetId, dungeon.DisplayName)
                : null)
            .OfType<IconSource>()
            .ToArray();

        return entity with
        {
            Rank = cdo is null ? null : Ranked(PropertyWalk.GetTags(cdo, CharacterTagsField)),
            Health = attributes?.Health,
            Armor = attributes?.Armor,
            Resist = attributes?.Resist,
            KillScore = attributes?.KillScore,
            SpiritPoints = attributes?.SpiritPoints,
            CountsTowardScore = cdo is not null &&
                PropertyWalk.Get(cdo, CountsTowardScoreField)?.GenericValue is false
                ? false
                : null,
            Intellect = attributes?.Intellect,
            Strength = attributes?.Strength,
            Agility = attributes?.Agility,
            Scaling = attributes?.Scaling,
            Abilities = Empty(Sourced(slots.Where(slot => !slot.Passive))),
            Passives = Empty(Sourced(slots.Where(slot => slot.Passive))),
            FoundIn = Empty(found),
        };
    }

    private IReadOnlyList<PhaseAbility> Sourced(IEnumerable<CreatureSlot> slots)
    {
        var held = new List<PhaseAbility>();

        foreach (var slot in slots)
        {
            var source = Owned(slot.Ability, AbilityTable)
                ?? Owned(slot.Ability, EffectTable)
                ?? Owned(slot.Ability, TalentTrees.AugmentationTable);

            if (source is null) continue;

            held.Add(new PhaseAbility(source, DocumentProjection.Spaced(slot.Phase), slot.Shown)
            {
                EnabledByDefault = slot.Timing?.EnabledByDefault,
                InitialCooldown = slot.Timing?.InitialCooldown,
                Cooldown = slot.Timing?.Cooldown,
                InterruptCooldown = slot.Timing?.InterruptCooldown,
                CCCooldown = slot.Timing?.CCCooldown,
                PostAbilityPadding = slot.Timing?.PostAbilityPadding,
            });
        }

        return held;
    }

    private static CreatureRank? Ranked(IReadOnlyList<string>? tags)
    {
        foreach (var (tag, rank) in Ranks)
        {
            if (tags?.Contains(tag, StringComparer.OrdinalIgnoreCase) is true) return rank;
        }

        return null;
    }

    private static readonly (string Tag, CreatureRank Rank)[] Ranks =
    [
        ("ActorTypeTags.Boss", CreatureRank.Boss),
        ("ActorTypeTags.Elite", CreatureRank.Elite),
        ("ActorTypeTags.FriendlyNPC", CreatureRank.Friendly),
        ("ActorTypeTags.Enemy", CreatureRank.Enemy),
    ];

    private Entity WithDungeon(Entity entity, Dungeon dungeon, IconWriter? icons)
    {
        var layout = Layouts.For(dungeon.Tag);

        return entity with
        {
            DisplayName = entity.DisplayName ?? dungeon.DisplayName,
            Description = entity.Description ?? dungeon.Description,
            Icon = entity.Icon ?? IconName(dungeon.BannerPath, icons),
            Background = IconName(dungeon.BackgroundPath, icons),
            MaxPlayers = dungeon.MaxPlayers,
            QualifierTime = dungeon.QualifierTime,
            RequiredScore = dungeon.RequiredScore,
            MinimumHeroRank = dungeon.MinimumHeroRank,
            LevelType = dungeon.LevelType,
            SeasonalPinnacle = dungeon.SeasonalPinnacle,
            AvailableForCasual = dungeon.AvailableForCasual,
            MinimumDifficulty = dungeon.MinimumDifficulty,
            EnabledInGame = Playable(dungeon) ? entity.EnabledInGame : false,
            Difficulties = DungeonDifficulties.For(dungeon.Tag),
            Floors = layout is null
                ? null
                : Empty([.. layout.Floors.Select(floor => new DungeonFloor(
                    floor.Name, IconName(floor.MapPath, icons), floor.Center, floor.Extent))]),
            Placements = layout is null
                ? null
                : Empty([.. layout.Placements.Select(placed => Owned(placed.Creature, CreatureTable) is { } creature
                        ? new PlacedCreature(creature, placed.Position)
                        {
                            Floor = placed.Floor,
                            MapX = placed.MapX,
                            MapY = placed.MapY,
                            Patrol = placed.Patrol,
                        }
                        : null)
                    .OfType<PlacedCreature>()]),
            Patrols = layout is null ? null : Empty(layout.Patrols),
        };
    }

    private Entity WithHero(Entity entity, HeroProfile hero, IconWriter? icons)
    {
        return entity with
        {
            DisplayName = entity.DisplayName ?? hero.ShippedName,
            PrimaryStat = hero.PrimaryStat,
            HeroColor = Settings.Hero(hero.ShippedName)?.Color,
            HeroTitle = hero.Title,
            Biography = hero.Biography,
            Playstyle = hero.Playstyle,
            SpiritAbility = hero.SpiritAbility,
            Role = hero.Role,
            RoleIcon = IconName(hero.RoleIconPath, icons),
            OverallDifficulty = hero.OverallDifficulty,
            Offense = hero.Offense,
            Defense = hero.Defense,
            Control = hero.Control,
            BaseAttributes = HeroAttributes.For(hero.Codename),
            Icon = entity.Icon ?? IconName(hero.PortraitPath, icons),
        };
    }

    private Entity WithModifier(Entity entity, Modifier modifier, IconWriter? icons) => entity with
    {
        DisplayName = entity.DisplayName ?? modifier.DisplayName,
        Description = entity.Description ?? modifier.Description,
        Icon = entity.Icon ?? IconName(modifier.IconPath, icons),
        Placeholders = entity.Placeholders ?? ModifierPlaceholders(modifier),
        ModifierKind = modifier.Kind,
        MinimumHeroRank = modifier.MinimumHeroRank,
    };

    private Dictionary<string, Placeholder>? ModifierPlaceholders(Modifier modifier)
    {
        if (modifier.Row is null) return null;

        var resolved = DescriptionTokens.Align(
            DescriptionTokens.Resolve(
                modifier.Row, _provider, _constants, _presets, null, null, _unbindableSpreadRows, Heroes),
            modifier.Description);

        return resolved.Count == 0 ? null : resolved;
    }

    private IReadOnlyList<IconSource> Granting(string augmentation) =>
        [.. Owners.GrantedBy(augmentation)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(named => Owned(named, AbilityTable) ?? Owned(named, EffectTable))
            .OfType<IconSource>()];

    private IconSource? Owned(string? named, string table)
    {
        if (named is null or AbilityOwners.Contested) return null;
        if (FindTag(table, named) is not { } tag) return null;

        return new IconSource(table, tag.NetId, named);
    }

    private static bool IsDynamicDefinition(FAssetData? asset) =>
        asset?.AssetClass.Text.Equals(DynamicDefinitionClass, StringComparison.OrdinalIgnoreCase) is true;

    private Entity WithProvenance(Entity entity, TagRecord tag, FAssetData? asset, UObject? cdo)
    {
        var carried = Sources.Fixed(tag.Name, out var granted);

        if (carried.Count > 0) return entity with { Sources = [.. carried], ItemLevel = granted };

        if (cdo is null) return entity;
        if (!IsDynamicDefinition(asset)) return entity;

        var tiers = DynamicVisuals.Tiers(cdo);
        var top = _rarities.Tiers.Max();

        var listed = DropsIn(cdo);
        var rotating = listed.Where(InRotation).ToArray();

        IReadOnlyList<ItemSource> dropped = listed.Count > 0 && rotating.Length == 0
            ? []
            : Sources.Dropped(rotating, level => FindTag(TypeResolver.LevelTable, level)?.NetId);

        return entity with
        {
            Sources = Empty([.. dropped, .. Challenges.ForItem(tag.Name)]),
            ItemLevel = Sources.DropLevel(tiers.Count > 0 && tiers.All(tier => tier == top)),
        };
    }

    private bool Playable(Dungeon dungeon) =>
        dungeon.EnabledInGame &&
        (dungeon.LevelType is not (DungeonLevelType or AdventureLevelType) ||
         dungeon.SeasonalPinnacle is true ||
         InRotation(dungeon.Tag));

    private bool InRotation(string levelTag) =>
        Dungeons.Find(levelTag)?.AvailableForCasual is true || DungeonDifficulties.For(levelTag) is { Count: > 0 };

    private static IReadOnlyList<string> DropsIn(UObject definition) =>
        PropertyWalk.Get(definition, SpecificDungeonsField)?.GenericValue is UScriptArray listed
            ? [.. listed.Properties.Select(entry => PropertyWalk.AsString(entry?.GenericValue)).OfType<string>()]
            : [];

    private Entity WithFixedItem(Entity entity, HeroItem item, IconWriter? icons)
    {
        var texture = entity.Icon is null ? item.IconPath : null;

        return entity with
        {
            DisplayName = entity.DisplayName ?? item.DisplayName,
            Slot = item.Slot ?? entity.Slot,
            ArmorType = item.ArmorType,
            EnabledInGame = item.EnabledInGame ? entity.EnabledInGame : false,
            Stats = entity.Stats ?? Empty(item.Stats),
            UniqueEquipped = item.UniqueEquipped ? true : null,
            Rarities = entity.Rarities ?? (item.Rarity is { } rarity ? [rarity] : null),
            Heroes = Empty([.. item.Heroes.Select(hero => Heroes.Shipped(hero) ?? hero)]),
            Icon = entity.Icon ?? IconName(texture, icons, item.Rarity),
            IconByRarity = entity.IconByRarity ?? TextureByRarity(texture, icons),
        };
    }

    private static Entity WithTalent(Entity entity, Talent talent, IconWriter? icons) => entity with
    {
        DisplayName = talent.DisplayName ?? entity.DisplayName,
        Description = talent.Description ?? entity.Description,
        Icon = IconName(talent.IconPath, icons) ?? entity.Icon,
        Heroes = [talent.HeroName],
        TalentSlot = talent.Slot,
        TalentRow = talent.Row,
        TalentColumn = talent.Column,
        TalentCost = talent.Cost,
    };

    private static IReadOnlyDictionary<string, Placeholder> Overlaid(
        IReadOnlyDictionary<string, Placeholder>? declared, IReadOnlyDictionary<string, Placeholder> bound)
    {
        var placeholders = new Dictionary<string, Placeholder>(StringComparer.Ordinal);

        foreach (var (key, placeholder) in declared ?? bound) placeholders[key] = placeholder;
        foreach (var (key, placeholder) in bound) placeholders[key] = placeholder;

        return placeholders;
    }

    private Entity WithGrantorText(
        Entity entity, string name, string? description, string? iconPath, IconWriter? icons) => entity with
    {
        DisplayName = entity.DisplayName ?? name,
        Description = entity.Description ?? description,
        Icon = entity.Icon ?? IconName(iconPath, icons),
    };

    private Entity Body(
        UObject? cdo,
        string? assetName,
        Display display,
        IPropertyHolder? tokenSource,
        IconWriter? icons)
    {
        var bindings = cdo is null ? [] : CurveBindings.Collect(cdo, _constants);

        var custom = cdo is null ? CustomMagnitudes.None : MagnitudeCalculations.Of(cdo, _provider, _constants);
        if (custom.Duration is { } computedDuration) bindings.Insert(0, computedDuration);

        if (cdo is not null) bindings.InsertRange(0, TooltipPayload.Bindings(cdo, _constants));

        var tuning = TuningNamespace.For(assetName, _constants);
        var costing = false;

        if (tuning is null && TuningNamespace.FromBindings(bindings, _constants) is { } shared)
        {
            tuning = shared;
            costing = cdo is not null && ClassDefaults.NativeParent(cdo, _provider) == CostEffectClass;
            NamespaceFromBindings++;
        }

        var tokenHolder = tokenSource ?? cdo;

        var placeholders = tokenHolder is null
            ? []
            : DescriptionTokens.Align(
                DescriptionTokens.Resolve(
                    tokenHolder, _provider, _constants, _presets, tuning, assetName, _unbindableSpreadRows, Heroes),
                display.Description);

        var split = Numbers.Split(bindings, tuning);
        var scaling = new SortedSet<string>(StringComparer.Ordinal);

        var bands = new EffectSpreads();

        var ownBand = cdo is null ? null : _presets.Own(cdo, _constants, _unbindableSpreadRows);

        var appliedDurations = new List<CurveBinding>();

        var boundByEffect = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var carried = cdo is null ? null : TagNames.Split(cdo);
        var tested = new SortedSet<string>(StringComparer.Ordinal);
        var added = new SortedSet<string>(StringComparer.Ordinal);
        var gated = new Dictionary<string, List<EffectModifier>>(StringComparer.Ordinal);
        var testerBindings = new List<CurveBinding>(bindings);
        var declaredGates = new List<(string Prefix, string Tag)>(DeclaredGates(cdo));
        var gatedReads = new List<GatedRead>(TagGatedReads.Of(cdo));
        var effectBindings = new Dictionary<string, IReadOnlyList<CurveBinding>>(StringComparer.OrdinalIgnoreCase);
        var effectAdds = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var uses = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        var supplied = new Dictionary<string, string>(StringComparer.Ordinal);
        var ownRows = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var foreign = new SortedSet<ForeignRead>(Comparer<ForeignRead>.Create((a, b) =>
            string.CompareOrdinal($"{a.Tag}|{a.Owner}|{a.Property}", $"{b.Tag}|{b.Owner}|{b.Property}")));

        if (cdo is not null)
        {
            foreach (var attribute in GameplayEffects.Captures(cdo) ?? []) scaling.Add(attribute);
            foreach (var attribute in custom.Captures) scaling.Add(attribute);

            Gate(cdo, tested, added, gated);
            Use(cdo, bindings, uses, foreign, supplied);

            foreach (var calculator in custom.Calculators)
            {
                Use(calculator, CurveBindings.Collect(calculator, _constants), uses, foreign, supplied);
            }

            ownRows.UnionWith(uses.Keys);

            foreach (var effect in Effects(cdo))
            {
                Gate(effect.Default, tested, added, gated);
                Use(effect.Default, effect.Constants ?? [], uses, foreign, supplied);
                declaredGates.AddRange(DeclaredGates(effect.Default));
                testerBindings.AddRange(effect.Constants ?? []);
                gatedReads.AddRange(TagGatedReads.Of(effect.Default));

                if (effect.Default?.Class?.Name.Text is { } effectClass)
                {
                    var effectName = effectClass.EndsWith("_C", StringComparison.Ordinal) ? effectClass[..^2] : effectClass;

                    effectBindings.TryAdd(effectName, effect.Constants ?? []);
                    effectAdds.TryAdd(effectName, [.. TagNames.Of(effect.Default).Added.Where(TagNames.IsTalent)]);
                }

                if (effect.Spread is { } band) bands.Add(effect.Constants ?? [], band, effect.SpreadOverridden);
                if (effect.Constants is null && effect.CapturedAttributes is null) continue;

                foreach (var attribute in effect.CapturedAttributes ?? []) scaling.Add(attribute);

                foreach (var binding in effect.Constants ?? [])
                {
                    Numbers.Add(split, binding, tuning);

                    if (binding is { Values: null, Value: { } bound }) boundByEffect[binding.RowName] = bound;

                    if (binding is { Computed: true, Value: not null }) appliedDurations.Add(binding);
                }
            }

            foreach (var row in bands.Contested) _contestedBands.Add(row);
        }

        var claimed = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var placeholder in placeholders.Values)
        {
            foreach (var attribute in placeholder.ScalesOff ?? []) scaling.Add(attribute);

            foreach (var key in placeholder.Constants?.Keys ?? [])
            {
                if (split.Rest.Remove(key, out var value)) claimed[key] = value;
            }
        }

        var supersedes = appliedDurations
            .Where(binding => Supersedes(binding, tuning))
            .Select(binding => binding.Value!.Value)
            .Distinct()
            .ToArray();

        if (supersedes.Length == 1 && !split.ByName.ContainsKey(Numbers.Duration))
        {
            split.ByName[Numbers.Duration] = supersedes[0];
        }

        if (AbilityCosts.Of(cdo, _provider, _constants, tuning, SpentFrom(assetName)) is { } cost &&
            !split.ByName.ContainsKey(Numbers.Cost))
        {
            split.ByName[Numbers.Cost] = cost.Amount;
            if (cost.ReadsPool) _pooledCosts.Add(cdo!);
        }

        if (!costing) Supplement(split, tuning, boundByEffect);

        var grantedTags = cdo is null ? [] : PropertyWalk.GetTags(cdo, GrantedTagsField) ?? [];
        foreach (var tag in grantedTags) SupplementTalent(split, tag);

        var tagRows = grantedTags
            .Concat(added)
            .Select(tag => TuningNamespace.ForTalentTag(tag, _constants))
            .OfType<string>()
            .SelectMany(scope => _constants.AllUnder(scope).Select(record => record.Key))
            .ToArray();

        var charges = Charged(cdo);

        var cooldown = Number(split, "cooldown");
        var timing = cdo is null || cooldown is null ? null : _cooldowns.Of(cdo, charges.Charges);
        var category = Categories.Of(cdo);

        var detail = display.Detail.Where(line => line != display.Description).ToArray();
        var (baseline, talents) = Numbers.SplitTalents(split.Rest);

        var magnitudes = EffectModifiers.Of(cdo, _provider, _constants, _mappings, CapturedBy(assetName));
        ModifiersRead += magnitudes.Modifiers.Count;

        foreach (var (name, fields) in magnitudes.Talents)
        {
            if (!talents.TryGetValue(name, out var varied)) talents[name] = varied = new(StringComparer.Ordinal);

            foreach (var (field, value) in fields) varied[field] = value;
        }

        var reachable = Rejoined(baseline, claimed, split.Promoted);
        var taken = new HashSet<string>(claimed.Keys, StringComparer.Ordinal);

        var declared = cdo is null ? DynamicTextFloatBody.Empty : DynamicTextFloats.Of(cdo);
        var printed = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var name in placeholders.Keys.ToArray())
        {
            var placeholder = placeholders[name];
            if (placeholder.FromTable) continue;

            var own = placeholder.Constants is not null;

            foreach (var key in placeholder.Constants?.Keys ?? []) Printed(printed, name, key);
            var source = placeholder.Constants ?? reachable;

            var spent = new List<string>();

            if (Declared(placeholder, cdo, declared, bindings, spent) is { } bound)
            {
                foreach (var row in spent)
                {
                    taken.Add(TuningNamespace.Relative(row, tuning));
                    Printed(printed, name, row);
                }

                placeholders[name] = bound;
                DeclaredByBlueprint++;
                continue;
            }

            if (NativeTokenRows.Value(assetName, name, _constants) is { } linked)
            {
                placeholders[name] = placeholder with { Value = linked };
                StatedByNativeLink++;
                continue;
            }

            var wanted = placeholder.Quantity switch
            {
                TokenQuantity.Period => Numbers.Period,
                TokenQuantity.Duration => Numbers.Duration,
                _ => null,
            };

            var tickrate = placeholder.Quantity == TokenQuantity.Period ? true : (bool?)null;

            if (wanted is null)
            {
                if (Coefficients.From(source, own ? null : taken, placeholder.ScalesOff) is { } resolved)
                {
                    taken.Add(resolved.Key);
                    Printed(printed, name, resolved.Key);
                    placeholders[name] = placeholder with
                    {
                        Attribute = resolved.Attribute,
                        Coefficient = resolved.Value,
                        Spread = Band(
                            resolved.Key,
                            tuning,
                            bands,
                            ownBand,
                            placeholder.PresetSpread is { } tokenBand
                                ? new EffectBand(tokenBand, placeholder.PresetSpreadOverridden)
                                : null),
                    };
                    continue;
                }
            }
            else if (!own && Numbers.SingleField(reachable, split.Own, taken, wanted) is { } row)
            {
                taken.Add(row.Key);
                Printed(printed, name, row.Key);
                placeholders[name] = placeholder with { Value = row.Value, Tickrate = tickrate };
                continue;
            }
            else if (placeholder.FromOwnConstants is true && Numbers.Own(split, wanted) is { } reading)
            {
                placeholders[name] = placeholder with { Value = reading, Tickrate = tickrate };
                NamedByToken++;
                continue;
            }

            if (placeholder.Constants is { } selected && Single(selected) is { } value)
            {
                placeholders[name] = placeholder with { Value = value, Tickrate = tickrate };
            }
        }

        foreach (var name in placeholders.Keys.ToArray())
        {
            if (!Reductions.Prints(placeholders[name], magnitudes.Modifiers, _multiplyingOps)) continue;

            placeholders[name] = placeholders[name] with { Format = Reductions.Format };
            PrintedAsReduction++;
        }

        var undeclared = DescriptionTokens.Tokens(display.Description ?? string.Empty)
            .Where(written => !placeholders.ContainsKey(written))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (undeclared is [var only] && SingleMagnitude(magnitudes.Modifiers) is { } stated)
        {
            placeholders[only] = new Placeholder { Value = stated };
            SynthesizedFromModifier++;
        }

        foreach (var (_, held) in uses)
        {
            foreach (var use in held.ToArray())
            {
                if (!use.StartsWith(RowSinks.SinkUse, StringComparison.Ordinal)) continue;

                foreach (var (tag, assigned) in supplied)
                {
                    if (use.Contains($"[{tag}]", StringComparison.Ordinal)) held.Add(assigned);
                }
            }
        }

        var candidates = split.Rows
            .Concat(tagRows)
            .Concat(tuning is null ? [] : _constants.AllUnder(tuning).Select(record => record.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var (token, keys) in printed)
        {
            if (!placeholders.TryGetValue(token, out var shown)) continue;

            var flags = RowSinks.PrintedUse + Flags(shown);

            foreach (var key in keys)
            {
                foreach (var row in candidates)
                {
                    if (!row.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                        !row.EndsWith("." + key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!uses.TryGetValue(row, out var held)) uses[row] = held = new(StringComparer.Ordinal);
                    held.Add(flags);
                }
            }
        }

        var described = DescriptionTokens.Renamed(placeholders, display.Description, Heroes);

        return new Entity
        {
            DisplayName = display.Name,
            Tags = cdo is null ? null : Empty(AbilityTypes(cdo)),
            Category = category,
            Description = described,
            DescriptionsByHero = tokenHolder is null
                ? null
                : DescriptionTokens.TableRows(tokenHolder).Select(DynamicText.ForRow).OfType<IReadOnlyDictionary<string, string>>().FirstOrDefault(),
            Detail = Empty(detail),
            Icon = IconName(display.Icon, icons),
            CastTime = Number(split, "castTime"),
            ChannelTime = Number(split, "channelTime"),
            ChannelTick = Number(split, "channelTick"),
            Cooldown = cooldown,
            Charges = charges.Charges,
            ChargesByLevelType = charges.ByLevelType,
            TargetCountFalloff = Falloffs.Of(cdo),
            MaxTargets = Falloffs.MaxTargets(cdo),
            CooldownDurationScalesOff = timing?.DurationScalesOff,
            CooldownTimeRateScalesOff = timing?.TimeRateScalesOff,
            CooldownHasteGrantedBy = cdo is null || cooldown is null ? null : Empty(HasteGrantedBy(cdo, charges.Charges)),
            CooldownAccelerationGrantedBy = timing is null ? null : Empty(AccelerationGrantedBy(category)),
            Cost = Number(split, "cost"),
            Resource = display.Resource,
            Range = Number(split, "range"),
            MinRange = Number(split, "minRange"),
            Radius = Number(split, "radius"),
            Duration = Number(split, "duration"),
            Period = Number(split, "period"),
            CastTimeScalesWithHaste = CastTimeScalesWithHaste(cdo) ? true : null,
            TickrateIgnoresHaste = cdo is not null && GameplayEffects.IgnoresHaste(cdo) ? true : null,
            MaxStacks = MaxStacks(cdo),
            Infinite = Infinite(cdo) ? true : null,
            ScalesOff = scaling.Count == 0 ? null : [.. scaling],
            Coefficients = Stated(reachable, split.Own, taken, tuning, bands, ownBand, [.. scaling]),
            Modifiers = magnitudes.Modifiers.Count == 0 ? null : magnitudes.Modifiers,
            DurationGates = magnitudes.DurationGates.Count == 0 ? null : magnitudes.DurationGates,
            Placeholders = placeholders.Count == 0 ? null : placeholders,
            Generates = Empty(Gains.Of(cdo, assetName)),
            Tuning = tuning,
            Declares = Empty(Bound(bindings)),
            Constants = baseline.Count == 0 ? null : baseline,
            ConstantsByRank = Ranked(split.Rows),
            Talents = talents.Count == 0 ? null : Renamed(talents),
            TalentGates = cdo is null ? null : Empty(TalentGates(cdo, talents.Keys)),
            GrantedTags = Empty(grantedTags),
            Rows = Rows(split.Rows),
            TagRows = Rows(tagRows),
            NamespaceRows = tuning is null ? null : Rows(_constants.AllUnder(tuning).Select(record => record.Key)),
            TestedTags = tested.Count == 0 ? null : [.. tested],
            AddedTags = added.Count == 0 ? null : [.. added],
            HeldTags = carried is null ? null : Empty([.. carried.Held.Order(StringComparer.Ordinal)]),
            EffectAssetTags = carried is null ? null : Empty([.. carried.Asset.Order(StringComparer.Ordinal)]),
            AbilityTags = cdo is null ? null : Empty([.. PropertyWalk.GetTags(cdo, AbilityTagsField) ?? []]),
            OngoingTags = carried is null ? null : Empty([.. carried.Ongoing.Order(StringComparer.Ordinal)]),
            CooldownTags = cdo is null ? null : Empty([.. PropertyWalk.GetTags(cdo, CooldownTagsField) ?? []]),
            TriggerTags = Empty(AbilityLinks.Triggers(cdo)),
            SentEvents = Empty(AbilityLinks.Sent(cdo)),
            SpawnedActors = Empty(RowSinks.SpawnedActors(cdo)),
            BypassCostTags = Empty(AbilityLinks.Bypassed(cdo)),
            InstantTags = Empty(AbilityLinks.Instant(cdo)),
            LooseTags = Empty(AbilityLinks.Loose(cdo)),
            ProcEffects = AbilityLinks.ProcEffects(cdo) is { Count: > 0 } procs ? procs : null,
            ProcAssetTags = AbilityLinks.ProcAssetTags(cdo) is { Count: > 0 } marked ? marked : null,
            StackGrants = RowSinks.StackGrants(cdo) is { Count: > 0 } stacked
                ? [.. stacked.Select(grant => new StackGrant(grant.Effect, grant.Stacks, grant.Gates))]
                : null,
            CooldownTerms = Empty(CooldownTerms.Of(cdo, _provider, _constants)),
            Counts = custom.Calculators.Prepend(cdo).SelectMany(asset => CountReads.Of(asset))
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal) is { Count: > 0 } counts
                ? counts
                : null,
            GrantedAbilityNames = Empty(AbilityLinks.GrantedAbilities(cdo)),

            GrantedEffects = cdo is null ? null : Empty([.. ClassPaths(cdo, GrantedEffectsField).Select(ClassName)]),
            GatedConstants = GatedConstants(tested, testerBindings, declaredGates, gatedReads, effectBindings),
            GatedAdds = GatedAdds(gatedReads, effectAdds),
            GatedModifiers = gated.Count == 0
                ? null
                : gated.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlyList<EffectModifier>)entry.Value,
                    StringComparer.Ordinal),
            RowUses = uses.Count == 0
                ? null
                : uses.ToDictionary(
                    entry => entry.Key,
                    entry => (IReadOnlyList<string>)[.. entry.Value],
                    StringComparer.OrdinalIgnoreCase),
            ForeignReads = foreign.Count == 0 ? null : [.. foreign],
            OwnRows = ownRows.Count == 0 ? null : [.. ownRows],
            Replacements = AbilityReplacements.Of(cdo) is { Count: > 0 } replaced ? replaced : null,
        };
    }

    private static void Printed(Dictionary<string, SortedSet<string>> printed, string token, string key)
    {
        if (!printed.TryGetValue(token, out var keys)) printed[token] = keys = new(StringComparer.Ordinal);
        keys.Add(key);
    }

    private static string Flags(Placeholder placeholder) =>
        string.Join(
            ';',
            new[]
            {
                placeholder.Key ?? string.Empty,
                placeholder.Format is { } format ? $"format={format}" : null,
                placeholder.Percent is true ? "percent" : null,
                placeholder.Multiplies is true ? "multiplies" : null,
                placeholder.Unit is { } unit ? $"unit={unit}" : null,
                placeholder.Quantity != default ? $"quantity={placeholder.Quantity}" : null,
                placeholder.Attribute is { } attribute ? $"attribute={attribute}" : null,
                placeholder.Tickrate is true ? "tickrate" : null,
            }.OfType<string>());

    private void Use(
        UObject? asset,
        IReadOnlyList<CurveBinding> bindings,
        Dictionary<string, SortedSet<string>> uses,
        ISet<ForeignRead> foreign,
        Dictionary<string, string> supplied)
    {
        if (asset is null) return;

        var variables = RowSinks.Variables(asset);

        foreach (var binding in bindings)
        {
            if (binding.RowName.Equals("None", StringComparison.Ordinal)) continue;
            if (!uses.TryGetValue(binding.RowName, out var held)) uses[binding.RowName] = held = new(StringComparer.Ordinal);

            var declared = variables.Contains(RowSinks.Head(binding.Property));
            held.Add((declared ? RowSinks.VariableUse : RowSinks.PropertyUse) + binding.Property);
        }

        foreach (var slot in EffectModifiers.Uses(asset, _mappings))
        {
            var stated = $"{RowSinks.AttributeUse}{slot.Attribute}|{slot.Op}|{(slot.Executed ? RowSinks.Execution : RowSinks.Holder)}";
            if (slot.Required.Count > 0) stated += RowSinks.GateMark + string.Join(";", slot.Required);

            if (slot.SetByCallerTag is { } tag) supplied.TryAdd(tag, stated);
            if (slot.Row is not { } row) continue;
            if (!uses.TryGetValue(row, out var held)) uses[row] = held = new(StringComparer.Ordinal);

            held.Add(stated);
        }

        var sinks = RowSinks.Of(asset, LoadClassDefault);

        foreach (var (row, sunk) in RowSinks.Rows(sinks, bindings))
        {
            if (!uses.TryGetValue(row, out var held)) uses[row] = held = new(StringComparer.Ordinal);

            foreach (var sink in sunk) held.Add(RowSinks.SinkUse + sink);
        }

        var own = RowSinks.OwnerClass(asset.Class?.Name.Text);

        foreach (var sink in sinks)
        {
            if (sink.Owner is not { } owner || owner == own) continue;
            if (bindings.Any(binding => RowSinks.Binds(binding, sink.Property))) continue;

            foreach (var tag in sink.Gates.Where(gate => !gate.StartsWith(RowSinks.PerGate, StringComparison.Ordinal))) foreign.Add(new ForeignRead(tag, owner, sink.Property));
        }

        foreach (var read in TagGatedReads.Of(asset))
        {
            if (read is not { Owner: { } owner, Property: { } property }) continue;
            if (bindings.Any(binding => RowSinks.Binds(binding, property))) continue;

            foreign.Add(new ForeignRead(read.Tag, owner, property));
        }
    }

    private void Gate(
        UObject? asset, SortedSet<string> tested, SortedSet<string> added, Dictionary<string, List<EffectModifier>> gated)
    {
        if (asset is null) return;

        var tags = TagNames.Of(asset);
        tested.UnionWith(tags.Named);
        added.UnionWith(tags.Added.Where(TagNames.IsTalent));

        foreach (var (tag, modifiers) in EffectModifiers.Gated(asset, _provider, _constants, _mappings))
        {
            if (!gated.TryGetValue(tag, out var under)) gated[tag] = under = [];

            foreach (var modifier in modifiers)
            {
                if (!under.Contains(modifier)) under.Add(modifier);
            }
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? GatedConstants(
        IEnumerable<string> tested,
        IReadOnlyList<CurveBinding> bindings,
        IEnumerable<(string Prefix, string Tag)> declared,
        IEnumerable<GatedRead> reads,
        IReadOnlyDictionary<string, IReadOnlyList<CurveBinding>> effectBindings)
    {
        var gated = new Dictionary<string, SortedDictionary<string, double>>(StringComparer.Ordinal);
        var read = new HashSet<CurveBinding>();

        void Add(string tag, CurveBinding binding)
        {
            if (binding is not { Value: { } value, Values: null }) return;
            if (!gated.TryGetValue(tag, out var rows)) gated[tag] = rows = new(StringComparer.Ordinal);

            rows[binding.RowName] = value;
            read.Add(binding);
        }

        foreach (var gate in reads)
        {
            if (!TagNames.IsTalent(gate.Tag)) continue;

            if (gate.Property is { } property)
            {
                foreach (var binding in bindings)
                {
                    if (HeroNames.LastSegment(binding.Property).Equals(property, StringComparison.OrdinalIgnoreCase) ||
                        binding.Property.Equals(property, StringComparison.OrdinalIgnoreCase))
                    {
                        Add(gate.Tag, binding);
                    }
                }
            }

            if (gate.Effect is { } effect && effectBindings.TryGetValue(effect, out var applied))
            {
                foreach (var binding in applied) Add(gate.Tag, binding);
            }
        }

        var talents = tested
            .Where(tag => tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal))
            .Select(tag => (Tag: tag, Talent: HeroNames.LastSegment(tag)))
            .OrderByDescending(entry => entry.Talent.Length)
            .ToArray();

        var prefixes = declared
            .Where(gate => gate.Prefix.Length > MinimumGatePrefix)
            .OrderByDescending(gate => gate.Prefix.Length)
            .ToArray();

        foreach (var binding in bindings)
        {
            if (read.Contains(binding)) continue;

            var property = Normalized(binding.Property);

            var tag = prefixes.FirstOrDefault(gate => property.StartsWith(gate.Prefix, StringComparison.Ordinal)).Tag
                ?? talents.FirstOrDefault(entry =>
                    property.Contains(entry.Talent, StringComparison.OrdinalIgnoreCase)).Tag;

            if (tag is not null) Add(tag, binding);
        }

        return gated.Count == 0
            ? null
            : gated.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyDictionary<string, double>)entry.Value,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? GatedAdds(
        IEnumerable<GatedRead> reads, IReadOnlyDictionary<string, string[]> effectAdds)
    {
        var adds = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var gate in reads)
        {
            if (gate.Effect is not { } effect || !effectAdds.TryGetValue(effect, out var added) || added.Length == 0)
            {
                continue;
            }

            if (!adds.TryGetValue(gate.Tag, out var under)) adds[gate.Tag] = under = new(StringComparer.Ordinal);
            under.UnionWith(added);
        }

        return adds.Count == 0
            ? null
            : adds.ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)[.. entry.Value],
                StringComparer.Ordinal);
    }

    private static IEnumerable<(string Prefix, string Tag)> DeclaredGates(UObject? asset)
    {
        if (asset is null) yield break;

        foreach (var property in PropertyWalk.Walk(asset))
        {
            if (!property.Name.EndsWith(TagSuffix, StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } tag) continue;
            if (!TagNames.IsTalent(tag)) continue;

            yield return (Normalized(property.Name[..^TagSuffix.Length]), tag);
        }
    }

    private static string Normalized(string property) =>
        property.Replace(" ", string.Empty).ToLowerInvariant();

    private IReadOnlyDictionary<string, double>? Rows(IEnumerable<string> reached)
    {
        var rows = new SortedDictionary<string, double>(StringComparer.Ordinal);

        foreach (var row in reached)
        {
            if (_constants.Lookup(row).Record is { Key: { } key, Value: { } value }) rows[key] = value;
        }

        return rows.Count == 0 ? null : rows;
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Renamed(
        Dictionary<string, Dictionary<string, object?>> talents) =>
        talents.ToDictionary(
            talent => Heroes.Renamed(talent.Key),
            talent => (IReadOnlyDictionary<string, object?>)talent.Value.ToDictionary(
                field => Heroes.Renamed(field.Key), field => field.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static bool Supersedes(CurveBinding binding, string? tuning)
    {
        if (tuning is null) return false;

        foreach (var row in binding.ComputedFrom ?? [])
        {
            var relative = TuningNamespace.Relative(row, tuning);
            if (relative.Length >= row.Length || relative.Contains('.')) continue;
            if (Numbers.Field(relative) == Numbers.Duration) return true;
        }

        return false;
    }

    private int? MaxStacks(UObject? cdo)
    {
        if (cdo is null) return null;

        var stacking = PropertyWalk.GetString(cdo, "StackingType");
        if (stacking is null || stacking.EndsWith("::None", StringComparison.Ordinal)) return null;

        if (PropertyWalk.GetStruct(cdo, "StackCountMax") is not { } stack) return null;

        var value = PropertyWalk.AsNumber(PropertyWalk.Get(stack, "Value")?.GenericValue);
        var row = PropertyWalk.GetStruct(stack, "Curve") is { } curve
            ? PropertyWalk.GetString(curve, "RowName")
            : null;

        var count = row is { Length: > 0 } && !row.Equals("None", StringComparison.Ordinal) &&
                    _constants.Lookup(row).Record?.Value is { } resolved
            ? (value ?? 1) * resolved
            : value;

        return count is > 1 ? (int)count : null;
    }

    private static bool Infinite(UObject? cdo) =>
        cdo is not null &&
        PropertyWalk.GetString(cdo, "DurationPolicy")?.EndsWith("::Infinite", StringComparison.Ordinal) == true;

    public int DeclaredByBlueprint { get; private set; }

    public int NamedByToken { get; private set; }

    public int StatedByNativeLink { get; private set; }

    public int PrintedAsReduction { get; private set; }

    public int SynthesizedFromModifier { get; private set; }

    public int ModifiersRead { get; private set; }

    public int DurationsFromSetByCaller { get; private set; }

    private static double? SingleMagnitude(IReadOnlyList<EffectModifier> modifiers) =>
        modifiers.Count == 1 ? modifiers[0].Value : null;

    private Placeholder? Declared(
        Placeholder placeholder,
        UObject? cdo,
        DynamicTextFloatBody declared,
        IReadOnlyList<CurveBinding> bindings,
        ICollection<string> spent)
    {
        if (placeholder.FromOwnConstants is not true) return null;
        if (placeholder.DynamicTextFloatIdentifier is not { } identifier) return null;

        var read = new List<string>();
        var borrowed = Borrowed(cdo);

        if (declared.Read(identifier, Reader(bindings, read), borrowed) is { } reading)
        {
            foreach (var row in read) spent.Add(row);

            return reading.IsNumber
                ? placeholder with
                {
                    Value = reading.Coefficient,
                    Tickrate = reading.ScalesWithHaste ? true : null,
                }
                : placeholder with
                {
                    Coefficient = reading.Coefficient,
                    Attribute = reading.Attribute ?? placeholder.Attribute,
                    ScalesWithExpertise = reading.ScalesWithExpertise ? true : null,
                    Bracket = ToBracket(reading),
                    Tickrate = reading.ScalesWithHaste ? true : null,
                };
        }

        if (cdo is null) return null;

        read.Clear();

        if (DynamicTextRanges.For(cdo, identifier, Reader(bindings, read), borrowed) is not { } range) return null;

        foreach (var row in read) spent.Add(row);

        return placeholder with
        {
            Coefficient = range.Coefficient,
            Attribute = range.Attribute ?? placeholder.Attribute,
            Spread = range.Spread,
            ScalesWithExpertise = range.ScalesWithExpertise ? true : null,
            Bracket = ToBracket(range),
        };
    }

    private const string DefaultPrefix = "Default__";

    private Func<string, string, double?>? Borrowed(UObject? cdo) =>
        cdo is null
            ? null
            : (holder, property) =>
            {
                if (PropertyWalk.GetString(cdo, holder) is not { } classPath) return null;
                if (!_provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package))
                {
                    return null;
                }

                var referenced = package.GetExports()
                    .FirstOrDefault(e => e.Name.StartsWith(DefaultPrefix, StringComparison.Ordinal));

                return referenced is null
                    ? null
                    : Scalable(CurveBindings.Collect(referenced, _constants), property)?.Value;
            };

    private static PlaceholderBracket? ToBracket(DynamicTextReading reading) =>
        reading.Terms is not { Count: > 0 } terms
            ? null
            : new PlaceholderBracket(
                [.. terms.Select(term => new PlaceholderTerm(term.Attribute, term.Coefficient))],
                reading.Addend ?? 0);

    private static Func<string, double?> Reader(IReadOnlyList<CurveBinding> bindings, ICollection<string> read) =>
        property =>
        {
            if (Scalable(bindings, property) is not { } binding) return null;

            read.Add(binding.RowName);
            return binding.Value;
        };

    private static CurveBinding? Scalable(IReadOnlyList<CurveBinding> bindings, string property)
    {
        var wanted = Numbers.Normalize(property);

        var matched = bindings
            .Where(b => b.Value is not null && b.Values is null)
            .Where(b => Numbers.Normalize(Leaf(b.Property)) == wanted)
            .ToArray();

        return matched.Select(b => b.Value!.Value).Distinct().Count() == 1 ? matched[0] : null;
    }

    private static string Leaf(string property) => property[(property.LastIndexOf('.') + 1)..];

    private IReadOnlyList<AbilityCoefficient>? Stated(
        IReadOnlyDictionary<string, object?> reachable,
        IReadOnlySet<string> own,
        IReadOnlySet<string> taken,
        string? tuning,
        EffectSpreads bands,
        EffectBand? ownBand,
        IReadOnlyList<string> capturedAttributes)
    {
        var stated = Coefficients.All(reachable, own, taken);

        if (stated.Count == 0) return null;

        StatedCoefficients += stated.Count;

        return
        [
            .. stated.Select(coefficient => new AbilityCoefficient(
                Coefficients.Captured(coefficient.Attribute, capturedAttributes), coefficient.Value)
            {
                Part = coefficient.Part,
                Spread = Band(coefficient.Key, tuning, bands, ownBand, null),
            }),
        ];
    }

    public int StatedCoefficients { get; private set; }

    public int NamespaceFromBindings { get; private set; }

    private double? Band(
        string coefficientKey,
        string? tuning,
        EffectSpreads bands,
        EffectBand? ownBand,
        EffectBand? tokenBand)
    {
        var reading = Spread.For(coefficientKey, tuning, _constants, bands, ownBand, tokenBand);

        if (reading.Band is not null) _spreadSources.TryAdd(coefficientKey, reading.Overridden);
        return reading.Band;
    }

    private const double SingleCast = 1;

    private ChargeCounts Charged(UObject? cdo)
    {
        if (cdo is null) return ChargeCounts.None;

        var counts = MaximumCharges.Of(cdo, _constants, _mappings);

        return counts.Charges == SingleCast ? counts with { Charges = null } : counts;
    }

    private bool CastTimeScalesWithHaste(UObject? cdo) =>
        cdo is not null && ClassDefaults.Inherited(cdo, HasteScalingField, _provider)?.GenericValue is true;

    private const string HasteScalingField = "ScaleTimeWithHaste";

    private IReadOnlyList<int> HasteGrantedBy(UObject cdo, double? charges)
    {
        if (charges > 1) return [];
        if (_cooldowns.Charged(cdo)?.TimeRateScalesOff?.Contains(CooldownScaling.HasteAttribute) is not true) return [];

        return
        [
            .. MaximumCharges.Raising(cdo, _constants, _mappings)
                .SelectMany(tag => AugmentationsGranting().GetValueOrDefault(tag) ?? [])
                .Distinct()
                .Order(),
        ];
    }

    private IReadOnlyList<int> AccelerationGrantedBy(string? category) =>
        category is null ? [] : AugmentationsAccelerating().GetValueOrDefault(category) ?? [];

    private Dictionary<string, List<int>> AugmentationsGranting() => IndexAugmentations().ByGrantedTag;

    private Dictionary<string, List<int>> AugmentationsAccelerating() => IndexAugmentations().ByAcceleratedCategory;

    private AugmentationIndex IndexAugmentations()
    {
        if (_augmentations is { } built) return built;

        var granting = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var accelerating = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var tag in _tags)
        {
            if (!tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase)) continue;
            if (LoadDefaultObject(FindAsset(tag.Name)) is not { } augmentation) continue;

            foreach (var granted in PropertyWalk.GetTags(augmentation, GrantedTagsField) ?? [])
            {
                Index(granting, granted, tag.NetId);
            }

            foreach (var effect in ClassPaths(augmentation, GrantedEffectsField))
            {
                foreach (var cooldownType in _cooldowns.CooldownTypesAccelerated(effect))
                {
                    if (Categories.NamedBy(cooldownType) is { } category) Index(accelerating, category, tag.NetId);
                }
            }
        }

        return _augmentations = new AugmentationIndex(granting, accelerating);
    }

    private static void Index(Dictionary<string, List<int>> index, string key, int netId)
    {
        if (!index.TryGetValue(key, out var ids)) index[key] = ids = [];
        if (!ids.Contains(netId)) ids.Add(netId);
    }

    private static IEnumerable<string> ClassPaths(IPropertyHolder holder, string field)
    {
        if (PropertyWalk.Get(holder, field)?.GenericValue is not UScriptArray listed) yield break;

        foreach (var entry in listed.Properties)
        {
            if (PropertyWalk.AsString(entry?.GenericValue) is { } path && path.Contains('/')) yield return path;
        }
    }

    private sealed record AugmentationIndex(
        Dictionary<string, List<int>> ByGrantedTag, Dictionary<string, List<int>> ByAcceleratedCategory);

    private static Dictionary<string, string> TalentGates(UObject cdo, IEnumerable<string> groups)
    {
        var gates = new Dictionary<string, string>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase);

        foreach (var property in PropertyWalk.Walk(cdo))
        {
            if (!property.Name.EndsWith(TagSuffix, StringComparison.Ordinal)) continue;
            if (PropertyWalk.AsString(property.Value) is not { } tag) continue;
            if (!tag.StartsWith(TalentTagPrefix, StringComparison.Ordinal)) continue;

            var talent = HeroNames.LastSegment(tag);
            if (wanted.Contains(talent)) gates.TryAdd(talent, tag);
        }

        return gates;
    }

    private static IReadOnlyDictionary<string, string>? Empty(Dictionary<string, string> map) =>
        map.Count == 0 ? null : map;

    private void SupplementTalent(SplitNumbers split, string tag)
    {
        if (TuningNamespace.ForTalentTag(tag, _constants) is not { } scope) return;

        foreach (var record in _constants.AllUnder(scope))
        {
            if (!split.Rows.Add(record.Key)) continue;

            split.Rest[record.Key] = record.Values is { Length: > 1 } ranks ? ranks : (object?)record.Value;
            NamespaceSupplied++;
        }
    }

    public int NamespaceCorroborated { get; private set; }

    private static bool Corroborates(
        IReadOnlyDictionary<string, double> bound, ConstantRecord record, string leaf) =>
        Numbers.Field(leaf) is not null &&
        record.Value is { } declared &&
        bound.TryGetValue(record.Key, out var applied) &&
        applied == declared;

    private void Supplement(SplitNumbers split, string? tuning, IReadOnlyDictionary<string, double> bound)
    {
        if (tuning is null) return;

        foreach (var record in _constants.DirectlyUnder(tuning))
        {
            var leaf = record.Key[(tuning.Length + 1)..];

            var claimed = split.Rows.Contains(record.Key);
            if (claimed && !Corroborates(bound, record, leaf)) continue;
            var binding = new CurveBinding(leaf, record.Key)
            {
                Value = record.Value,
                Values = record.Values is { Length: > 1 } ranks ? ranks : null,
                ConstantsTable = record.Table,
            };

            var one = Numbers.Split([binding], tuning);
            split.Rows.Add(record.Key);

            foreach (var (field, value) in one.ByName)
            {
                if (!split.ByName.TryAdd(field, value)) continue;

                if (claimed) NamespaceCorroborated++; else NamespaceSupplied++;

                foreach (var (key, row) in one.Promoted)
                {
                    split.Promoted[key] = row;
                    split.Own.Add(key);
                }
            }

            foreach (var (key, value) in one.Rest)
            {
                if (!split.Rest.TryAdd(key, value)) continue;

                split.Own.Add(key);
                NamespaceSupplied++;
            }
        }
    }

    private static double? Number(SplitNumbers split, string field) =>
        split.ByName.TryGetValue(field, out var value) ? value : null;

    private static IReadOnlyDictionary<string, object?> Rejoined(
        Dictionary<string, object?> baseline,
        Dictionary<string, object?> claimed,
        IReadOnlyDictionary<string, double> promoted)
    {
        if (claimed.Count == 0 && promoted.Count == 0) return baseline;

        var all = new Dictionary<string, object?>(baseline, StringComparer.Ordinal);
        foreach (var (key, value) in claimed) all[key] = value;
        foreach (var (key, value) in promoted) all.TryAdd(key, value);

        return Numbers.SplitTalents(all).Baseline;
    }

    private static double? Single(IReadOnlyDictionary<string, object?> constants)
    {
        double? only = null;

        foreach (var value in constants.Values)
        {
            if (Flat(value) is not { } number) continue;
            if (only is not null) return null;
            only = number;
        }

        return only;
    }

    private static double? Flat(object? value) => value switch
    {
        double number => number,
        double?[] { Length: > 0 } ranks when ranks[0] is { } first && Array.TrueForAll(ranks, step => step == first) =>
            first,
        _ => null,
    };

    internal static Entity FillFrom(Entity outer, IReadOnlyList<Entity> inner)
    {
        var filled = outer;
        foreach (var ability in inner) filled = Fill(filled, ability);

        var cooldown = Agreed(outer.Cooldown, inner, ability => ability.Cooldown);

        return filled with
        {
            CastTime = Agreed(outer.CastTime, inner, ability => ability.CastTime),
            ChannelTime = Agreed(outer.ChannelTime, inner, ability => ability.ChannelTime),
            ChannelTick = Agreed(outer.ChannelTick, inner, ability => ability.ChannelTick),
            Cooldown = cooldown,
            CooldownDurationScalesOff = cooldown is null ? null : filled.CooldownDurationScalesOff,
            CooldownTimeRateScalesOff = cooldown is null ? null : filled.CooldownTimeRateScalesOff,
            CooldownHasteGrantedBy = cooldown is null ? null : filled.CooldownHasteGrantedBy,
            CooldownAccelerationGrantedBy = cooldown is null ? null : filled.CooldownAccelerationGrantedBy,
            Charges = Agreed(outer.Charges, inner, ability => ability.Charges),
            Cost = Agreed(outer.Cost, inner, ability => ability.Cost),
            Range = Agreed(outer.Range, inner, ability => ability.Range),
            MinRange = Agreed(outer.MinRange, inner, ability => ability.MinRange),
            Radius = Agreed(outer.Radius, inner, ability => ability.Radius),
            Duration = Agreed(outer.Duration, inner, ability => ability.Duration),
            Period = Agreed(outer.Period, inner, ability => ability.Period),
        };
    }

    private static double? Agreed(double? own, IReadOnlyList<Entity> granted, Func<Entity, double?> read)
    {
        if (own is not null) return own;

        double? agreed = null;

        foreach (var ability in granted)
        {
            if (read(ability) is not { } value) continue;
            if (agreed is { } first && first != value) return null;

            agreed = value;
        }

        return agreed;
    }

    private static Entity Fill(Entity outer, Entity inner) => outer with
    {
        DisplayName = outer.DisplayName ?? inner.DisplayName,
        Description = outer.Description ?? inner.Description,
        Detail = outer.Detail ?? inner.Detail,
        Icon = outer.Icon ?? inner.Icon,
        Tags = outer.Tags ?? inner.Tags,
        Category = outer.Category ?? inner.Category,
        ChargesByLevelType = outer.ChargesByLevelType ?? inner.ChargesByLevelType,
        TargetCountFalloff = Union(outer.TargetCountFalloff, inner.TargetCountFalloff),
        MaxTargets = outer.MaxTargets ?? inner.MaxTargets,
        Resource = outer.Resource ?? inner.Resource,
        CastTimeScalesWithHaste = outer.CastTimeScalesWithHaste ?? inner.CastTimeScalesWithHaste,
        CooldownDurationScalesOff = outer.CooldownDurationScalesOff ?? inner.CooldownDurationScalesOff,
        CooldownTimeRateScalesOff = outer.CooldownTimeRateScalesOff ?? inner.CooldownTimeRateScalesOff,
        CooldownHasteGrantedBy = outer.CooldownHasteGrantedBy ?? inner.CooldownHasteGrantedBy,
        CooldownAccelerationGrantedBy = outer.CooldownAccelerationGrantedBy ?? inner.CooldownAccelerationGrantedBy,
        ScalesOff = Union(outer.ScalesOff, inner.ScalesOff),
        Coefficients = Union(outer.Coefficients, inner.Coefficients),
        Generates = Union(outer.Generates, inner.Generates),
        Tuning = outer.Tuning ?? inner.Tuning,
        Declares = Union(outer.Declares, inner.Declares),
        Placeholders = Union(outer.Placeholders, inner.Placeholders),
        Constants = Union(outer.Constants, inner.Constants),
        ConstantsByRank = Union(outer.ConstantsByRank, inner.ConstantsByRank),
        Talents = UnionTalents(outer.Talents, inner.Talents),
        TalentGates = Union(outer.TalentGates, inner.TalentGates),
        GrantedTags = Union(outer.GrantedTags, inner.GrantedTags),
        DurationGates = Union(outer.DurationGates, inner.DurationGates),
        Rows = Union(outer.Rows, inner.Rows),
        TagRows = Union(outer.TagRows, inner.TagRows),
        NamespaceRows = Union(outer.NamespaceRows, inner.NamespaceRows),
        GatedAdds = Union(outer.GatedAdds, inner.GatedAdds),
        TestedTags = Union(outer.TestedTags, inner.TestedTags),
        AddedTags = Union(outer.AddedTags, inner.AddedTags),
        GatedConstants = Union(outer.GatedConstants, inner.GatedConstants),
        GatedModifiers = Union(outer.GatedModifiers, inner.GatedModifiers),
        RowUses = UnionUses(outer.RowUses, inner.RowUses),
        OwnRows = Union(outer.OwnRows, inner.OwnRows),
        Replacements = Union(outer.Replacements, inner.Replacements),
        CooldownTerms = outer.CooldownTerms ?? inner.CooldownTerms,
        ForeignReads = outer.ForeignReads is null || inner.ForeignReads is null
            ? outer.ForeignReads ?? inner.ForeignReads
            : [.. outer.ForeignReads.Concat(inner.ForeignReads).Distinct()],
    };

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? UnionTalents(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? first,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? second)
    {
        if (first is null || second is null) return first ?? second;

        var merged = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var source in new[] { first, second })
        {
            foreach (var (talent, fields) in source)
            {
                if (merged.TryGetValue(talent, out var existing))
                {
                    merged[talent] = Union(existing, fields)!;
                    continue;
                }
                merged[talent] = fields;
            }
        }

        return merged;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? UnionUses(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? first,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? second)
    {
        if (first is null || second is null) return first ?? second;

        var merged = new Dictionary<string, IReadOnlyList<string>>(first, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in second)
        {
            merged[key] = merged.TryGetValue(key, out var held) ? Union(held, value)! : value;
        }

        return merged;
    }

    private static IReadOnlyList<string>? Union(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        if (first is null || second is null) return first ?? second;
        return [.. first.Concat(second).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static IReadOnlyList<AbilityCoefficient>? Union(
        IReadOnlyList<AbilityCoefficient>? first, IReadOnlyList<AbilityCoefficient>? second)
    {
        if (first is null || second is null) return first ?? second;
        return [.. first.Concat(second).Distinct()];
    }

    private static IReadOnlyList<TargetCountFalloffSource>? Union(
        IReadOnlyList<TargetCountFalloffSource>? first, IReadOnlyList<TargetCountFalloffSource>? second)
    {
        if (first is null || second is null) return first ?? second;
        return [.. first.Concat(second).Distinct()];
    }

    private static IReadOnlyList<ResourceGain>? Union(
        IReadOnlyList<ResourceGain>? first, IReadOnlyList<ResourceGain>? second)
    {
        if (first is null || second is null) return first ?? second;
        return ResourceGains.Ordered(ResourceGains.Collapsed(first.Concat(second).Distinct()));
    }

    private static IReadOnlyDictionary<string, T>? Union<T>(
        IReadOnlyDictionary<string, T>? first, IReadOnlyDictionary<string, T>? second)
    {
        if (first is null || second is null) return first ?? second;

        var merged = new Dictionary<string, T>(first, StringComparer.Ordinal);
        foreach (var (key, value) in second) merged.TryAdd(key, value);
        return merged;
    }

    private (Entity? Entity, string? Path) ResolveGranted(
        UObject? cdo, List<Variant> variants, bool restricted, IconWriter? icons)
    {
        if (cdo is null) return (null, null);
        if (PropertyWalk.GetString(cdo, MappingKeyField) is not { } key) return (null, null);

        var matches = Mappings.FindAll(key);
        if (matches.Count == 0) return (null, null);

        var distinct = matches.Select(m => m.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinct.Length == 1) return (LoadAugmentation(distinct[0], icons), distinct[0]);

        foreach (var (hero, assetPath) in matches)
        {
            var variant = variants.FirstOrDefault(v => v.Hero.Equals(hero, StringComparison.OrdinalIgnoreCase));
            if (variant is null)
            {
                if (restricted) continue;
                variant = new Variant(hero) { HeroName = Heroes.Shipped(hero) };
                variants.Add(variant);
            }

            variant.Grants = LoadAugmentation(assetPath, icons);
            variant.GrantsId = _augmentationIds.For(assetPath);
        }

        variants.Sort((a, b) => string.Compare(a.HeroName ?? a.Hero, b.HeroName ?? b.Hero, StringComparison.OrdinalIgnoreCase));
        return (null, null);
    }

    private List<EffectRef> Effects(UObject asset) =>
        [.. GameplayEffects.Reached(asset, _effectNames)
            .Select(reference => GameplayEffects.Resolve(
                _provider, _constants, _presets, Locate(reference.Name, reference.Path), _unbindableSpreadRows))];

    private string Locate(string effectName, string path) =>
        path.Contains('/') ? path : FindAsset(effectName)?.ObjectPath ?? path;

    private static readonly string[] EffectTagFamilies = ["EffectType.", "EffectLogic.", "EffectVisualization."];

    private static List<string> EffectTags(UObject cdo) =>
        [.. (PropertyWalk.GetStruct(cdo, EffectTagsField) is { } inherited
                ? PropertyWalk.GetTags(inherited, CombinedTagsField) ?? []
                : [])
            .Where(tag => EffectTagFamilies.Any(family => tag.StartsWith(family, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

    private static List<string> AbilityTypes(UObject cdo) =>
        [.. (PropertyWalk.GetTags(cdo, "AbilityTags") ?? [])
            .Where(t => t.StartsWith(AbilityTypePrefix, StringComparison.Ordinal))
            .Select(t => t[AbilityTypePrefix.Length..])
            .Order(StringComparer.Ordinal)];

    private List<Entity> GrantedAbilities(UObject asset, IconWriter? icons) =>
        [.. PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith("GrantedAbilities[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => ResolveAbility(path, icons))];

    private static IEnumerable<string> LegendaryStatPaths(UObject cdo) =>
        PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>();

    private List<Entity> LegendaryStats(UObject cdo, IconWriter? icons) =>
        [.. LegendaryStatPaths(cdo).Select(path => LoadAugmentation(path, icons))];

    private List<int> LegendaryStatIds(UObject cdo) =>
        [.. LegendaryStatPaths(cdo).Select(_augmentationIds.For).OfType<int>()];

    private List<LegendaryStatText> UntaggedLegendaryStats(UObject cdo, IconWriter? icons) =>
        [.. LegendaryStatPaths(cdo)
            .Select((path, position) => (Path: path, Position: position))
            .Where(stat => _augmentationIds.For(stat.Path) is null)
            .Select(stat => (stat.Position, Loaded: LoadAugmentation(stat.Path, icons)))
            .Where(stat => stat.Loaded.DisplayName is not null)
            .Select(stat => new LegendaryStatText(stat.Position, stat.Loaded.DisplayName!)
            {
                Description = stat.Loaded.Description,
            })];

    public Entity LoadAugmentation(string assetPath, IconWriter? icons)
    {
        if (Load(assetPath) is not { } export) return new Entity();

        var own = Body(
            export,
            AbilityPresentationIndex.AssetName(assetPath),
            Display.From(null, export),
            null,
            icons);

        return FillFrom(own, GrantedAbilities(export, icons));
    }

    public Entity ResolveAbility(string assetPath, IconWriter? icons)
    {
        if (Load(assetPath) is not { } cdo) return new Entity();

        var name = AbilityPresentationIndex.AssetName(assetPath);
        var presentation = Presentation.Find(name);

        return Body(cdo, name, Display.From(presentation?.Row, cdo), presentation?.Row, icons);
    }

    private UObject? Load(string assetPath)
    {
        var package = AbilityPresentationIndex.PackagePath(assetPath);
        if (!_provider.TryLoadPackage(package, out var loaded)) return null;

        var exports = loaded.GetExports().ToArray();
        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }

    private HashSet<string> Characters()
    {
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in _tags)
        {
            if (tag.Table.Equals(HeroNames.Table, StringComparison.OrdinalIgnoreCase) &&
                tag.Name.StartsWith(HeroVariants.HeroTagPrefix, StringComparison.OrdinalIgnoreCase) &&
                tag.Name[HeroVariants.HeroTagPrefix.Length..] is var codename && !codename.Contains('.'))
            {
                named.Add(codename);
            }

            if (tag.Table.Equals(CreatureTable, StringComparison.OrdinalIgnoreCase) &&
                LoadDefaultObject(FindAsset(tag.Name)) is { } creature &&
                PropertyWalk.GetString(creature, CharacterIdField) is { } characterId)
            {
                named.Add(characterId);
            }
        }

        return named;
    }

    private static IEnumerable<string> Spellings(string name) =>
        new[] { name, name + "_C", name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name }
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private FAssetData? FindAsset(string name)
    {
        if (_byPrimaryAssetName.TryGetValue(name, out var primary)) return primary;
        if (_byAssetName.TryGetValue(name, out var byName)) return byName;
        return _petsByTag.GetValueOrDefault(name) ?? _mountsByTag.GetValueOrDefault(name);
    }

    private void IndexMounts()
    {
        foreach (var asset in _assets)
        {
            if (!asset.AssetClass.Text.Equals(MountClass, StringComparison.Ordinal)) continue;
            if (LoadDefaultObject(asset) is not { } mount) continue;
            if (PropertyWalk.GetString(mount, MountIdField) is not { } tag) continue;

            if (!_mountsByTag.TryAdd(tag, asset))
            {
                throw new InvalidOperationException(
                    $"Two {MountClass} assets declare {tag} as their {MountIdField}, so the mount it names has " +
                    "two definitions.");
            }
        }
    }

    private void IndexPets()
    {
        foreach (var asset in _assets)
        {
            if (!asset.AssetClass.Text.Equals("CRCompanionPetAsset", StringComparison.Ordinal)) continue;
            if (LoadDefaultObject(asset) is not { } pet) continue;
            if (PropertyWalk.GetString(pet, "PetID") is not { } tag) continue;
            if (tag.EndsWith(".None", StringComparison.OrdinalIgnoreCase)) continue;

            _petsByTag.TryAdd(tag, asset);
        }
    }

    private void IndexAssets()
    {
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in _assets)
        {
            if (AssetRegistry.TagValue(asset, "PrimaryAssetName") is { } primary)
            {
                _byPrimaryAssetName.TryAdd(primary, asset);
            }

            if (asset.AssetClass.Text.Equals(TextureClass, StringComparison.Ordinal)) continue;

            var name = asset.AssetName.Text;
            if (ambiguous.Contains(name)) continue;
            if (_byAssetName.TryAdd(name, asset)) continue;

            _byAssetName.Remove(name);
            ambiguous.Add(name);
        }
    }

    private UObject? LoadClassDefault(string classPath)
    {
        if (!_provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(classPath), out var package)) return null;

        return package.GetExports().FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
    }

    private UObject? LoadDefaultObject(FAssetData? asset)
    {
        if (asset is null) return null;

        var path = AbilityPresentationIndex.PackagePath(asset.ObjectPath);
        if (!_provider.TryLoadPackage(path, out var package)) return null;

        var exports = package.GetExports().ToArray();

        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault(e => e.Name.Equals(asset.AssetName.Text, StringComparison.OrdinalIgnoreCase))
            ?? exports.FirstOrDefault();
    }

    private static IReadOnlyList<T>? Empty<T>(IReadOnlyList<T> items) => items.Count == 0 ? null : items;

    private static string ClassName(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.IndexOf('.');

        return dot < 0 ? name : name[..dot];
    }

    private SortedDictionary<string, IReadOnlyList<double?>>? Ranked(IEnumerable<string> rows)
    {
        var ranked = new SortedDictionary<string, IReadOnlyList<double?>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (_constants.Lookup(row).Record is { Values.Length: > 1 } record) ranked[record.Key] = record.Values;
        }

        return ranked.Count == 0 ? null : ranked;
    }

    private IReadOnlyList<string> Bound(IReadOnlyList<CurveBinding> bindings) =>
    [
        .. bindings
            .Select(binding => binding.RowName)
            .Where(row => _constants.Lookup(row).Record is not null)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];
}
