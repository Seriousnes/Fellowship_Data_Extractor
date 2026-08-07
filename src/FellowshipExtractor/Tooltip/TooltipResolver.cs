using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

using FellowshipExtractor.Commands;

namespace FellowshipExtractor.Tooltip;

public delegate string? IconWriter(string iconObjectPath);

public sealed class TooltipResolver
{
    private const string MappingKeyField = "GrantedAbilityMappingKey";
    private const string RuleSetField = "RuleSet";
    private const string RuleSetPrefix = "DynamicHeroItemRuleSet.";
    private const string SetBonusKeyField = "SetBonusMappingKey";
    private const string VisualKeyField = "VisualCollectionMappingKey";
    private const string SlotOrdinalField = "HeroItemType";
    private const string EffectTable = "GameplayEffect";
    private const string AbilityTypePrefix = "AbilityType.";
    private const string TalentTagPrefix = "Talents.";
    private const string TagSuffix = "Tag";
    private const string GrantedTagsField = "GrantedGameplayTags";
    private const string ChargeEffectField = "ChargeGameplayEffectClass";
    private const string NativeChargeEffect = "CRChargeEffect";

    private readonly AbstractFileProvider _provider;
    private readonly FAssetData[] _assets;
    private readonly ConstantsTable _constants;
    private readonly EffectCalculationPresets _presets;

    private readonly Dictionary<string, bool> _spreadSources = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _contestedBands = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<TagRecord> _tags;
    private readonly AugmentationIds _augmentationIds;
    private readonly Dictionary<string, FAssetData> _byPrimaryAssetName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FAssetData> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _effectNames = new(StringComparer.OrdinalIgnoreCase);

    public AbilityPresentationIndex Presentation { get; }
    public EffectIconSources EffectIcons { get; }
    public EffectNameSources EffectNames { get; }
    public GrantedAbilityMappings Mappings { get; }
    public HeroNames Heroes { get; }
    public ItemSlots Slots { get; }
    public StatCalculation StatCalculation { get; }
    public ItemSlotNames SlotNames { get; }
    public SlotIcons SlotGlyphs { get; }

    public TemperCeilings Tempers { get; }
    public DropItemLevels Drops { get; }
    public ArmorTypes Armor { get; }
    public HeroItemTable HeroItems { get; }
    public DynamicItemVisuals DynamicVisuals { get; }
    public GrantedCharges Charges { get; }
    public ItemSetBonuses SetBonuses { get; }
    public ItemTableNames TableNames { get; }
    public DynamicItemRuleSets RuleSets { get; }
    public TalentTrees Talents { get; }
    public GemDefinitions Gems { get; }
    public ItemTraits Traits { get; }
    public Vocabulary Vocabulary { get; }
    public TypeResolver Types { get; }

    public int NamespaceSupplied { get; private set; }

    public int IconsInherited { get; private set; }

    public int NamesInherited { get; private set; }

    public int NamesContested { get; private set; }

    public int IconsContested { get; private set; }

    public int PresetCount => _presets.Count;

    public int SpreadOverrides => _spreadSources.Values.Count(fromRow => fromRow);

    public int SpreadPresetDefaults => _spreadSources.Values.Count(fromRow => !fromRow);

    public IReadOnlyCollection<string> SpreadContested => _contestedBands;

    public TooltipResolver(AbstractFileProvider provider)
    {
        _provider = provider;
        _assets = [.. RegistryCommand.ReadAssets(provider)];
        _tags = [.. CsvExportCommands.ReadTags(provider)];
        _constants = new ConstantsTable(provider);
        _presets = new EffectCalculationPresets(provider, _assets);

        IndexAssets();

        foreach (var tag in _tags)
        {
            if (tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) _effectNames.Add(tag.Name);
        }

        _augmentationIds = new AugmentationIds(_tags);
        var rarities = new RarityNames(provider, _assets);

        Presentation = new AbilityPresentationIndex(provider, _assets);
        Mappings = new GrantedAbilityMappings(provider, _assets);
        Slots = new ItemSlots(provider, _assets);
        SlotNames = new ItemSlotNames(provider, _assets);
        SlotGlyphs = new SlotIcons(provider, SlotNames);
        Tempers = new TemperCeilings(provider, rarities);
        Drops = new DropItemLevels(provider, _assets);
        Armor = new ArmorTypes(provider, _assets);
        SetBonuses = new ItemSetBonuses(provider, _assets, _augmentationIds);
        TableNames = new ItemTableNames(provider, _assets, rarities);
        RuleSets = new DynamicItemRuleSets(provider, _assets);
        Gems = new GemDefinitions(provider, _assets, _constants, _augmentationIds, rarities);
        Traits = new ItemTraits(
            provider, _assets, _constants, _augmentationIds, new TraitCategoryNames(provider, _assets));

        StatCalculation = new StatCalculation(provider, _assets, SlotNames, Armor);

        var collections = new VisualCollections(provider);
        HeroItems = new HeroItemTable(provider, _assets, Armor, SlotNames, collections, rarities);
        Heroes = new HeroNames(provider, _assets, Armor, HeroItems.PrimaryStatByHero);

        DynamicVisuals = new DynamicItemVisuals(provider, _assets, collections, rarities);

        Charges = new GrantedCharges(provider, _assets, Presentation);

        Talents = new TalentTrees(provider, _assets, _tags, Heroes);

        Vocabulary = new Vocabulary(
            provider, _assets, Heroes, rarities, StatCalculation, Slots.RandomStatPool, Gems.Types,
            SlotGlyphs, Tempers, Drops, new Blessings(provider, _constants));

        EffectIcons = new EffectIconSources(
            _tags, _effectNames, Presentation, tag => LoadDefaultObject(FindAsset(tag.Name)));

        EffectNames = new EffectNameSources(
            _tags, _effectNames, tag => LoadDefaultObject(FindAsset(tag.Name)), PrintedName);

        Types = new TypeResolver(_tags, Talents, Gems, Traits, SetBonuses);
    }

    private string? PrintedName(TagRecord tag, UObject cdo)
    {
        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            if (Talents.ForAugmentation(tag.Name)?.DisplayName is { } talent) return talent;
            if (Gems.ForAugmentation(tag.Name)?.DisplayName is { } node) return node;
        }

        return Display.From(Presentation.Find(tag.Name)?.Row, cdo).Name;
    }

    private static string ShortRuleSet(string tagName) =>
        tagName.StartsWith(RuleSetPrefix, StringComparison.OrdinalIgnoreCase)
            ? tagName[RuleSetPrefix.Length..]
            : tagName;

    public int ConstantCount => _constants.Count;

    public IReadOnlyList<TagRecord> Tags => _tags;

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

    public (int Covered, int Total) Coverage(string table)
    {
        int total = 0, covered = 0;
        foreach (var tag in _tags)
        {
            if (!tag.Table.Equals(table, StringComparison.OrdinalIgnoreCase)) continue;
            total++;
            if (Presentation.Find(tag.Name) is not null) covered++;
        }
        return (covered, total);
    }

    public IReadOnlyList<Entity> Resolve(TagRecord tag, IconWriter? icons)
    {
        var asset = FindAsset(tag.Name);
        var cdo = LoadDefaultObject(asset);
        var presentation = Presentation.Find(tag.Name);
        var display = Display.From(presentation?.Row, cdo);

        var variants = cdo is null ? [] : HeroVariants.Collect(cdo, Heroes);

        var restricted = variants.Count > 0;

        var body = Body(cdo, asset?.AssetName.Text ?? tag.Name, display, presentation, icons);
        if (cdo is not null) body = FillFrom(body, GrantedAbilities(cdo, icons));

        var (granted, grantedPath) = ResolveGranted(cdo, variants, restricted, icons);

        var ruleSet = cdo is null ? null : PropertyWalk.GetString(cdo, RuleSetField);

        var entity = body with
        {
            Table = tag.Table,
            NetId = tag.NetId,
            Name = tag.Name,
            Slot = SlotNames.FromTag(tag.Name)?.Shipped,
            GemSlot = RuleSets.HasGemSocket(ruleSet) ? true : null,
            RuleSet = ruleSet is null ? null : ShortRuleSet(ruleSet),
            Rarities = cdo is null ? null : Empty(DynamicVisuals.Rarities(cdo)),
            Stats = cdo is null ? null : Empty(Slots.For(cdo, StatCalculation)),
            Grants = granted,
            GrantsId = _augmentationIds.For(grantedPath),
            LegendaryStats = cdo is null ? null : Empty(LegendaryStats(cdo, icons)),
            LegendaryStatIds = cdo is null ? null : Empty(LegendaryStatIds(cdo)),
            Variants = Empty(variants),
        };

        entity = WithTableText(entity, tag.Name, icons);

        if (HeroItems.Find(tag.Name) is { } fixedItem) entity = WithFixedItem(entity, fixedItem, icons);

        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            if (Talents.ForAugmentation(tag.Name) is { } talent) entity = WithTalent(entity, talent, icons);
            if (Gems.ForAugmentation(tag.Name) is { } node) entity = WithGemNode(entity, node, icons);
        }

        entity = entity with { Type = Types.Of(tag, entity, asset?.PackageName.Text) };


        if (entity.DisplayName is null)
        {
            if (EffectNames.For(tag.Name) is { } named)
            {
                entity = entity with { DisplayName = named.DisplayName, NameInheritedFrom = named.Origin };
                NamesInherited++;
            }
            else if (EffectNames.Disagrees(tag.Name))
            {
                NamesContested++;
            }
        }

        if (entity is { Icon: null, DisplayName: not null })
        {
            if (EffectIcons.For(tag.Name) is { } source && Written(source.IconPath, icons) is { } file)
            {
                entity = entity with { Icon = file, IconInheritedFrom = source.Origin };
                IconsInherited++;
            }
            else if (EffectIcons.Disagrees(tag.Name))
            {
                IconsContested++;
            }
        }

        if (tag.Table.Equals(TalentTrees.SlotTable, StringComparison.OrdinalIgnoreCase) &&
            Talents.InSlot(tag.NetId) is { Count: > 0 } occupants)
        {
            return TalentTrees.Expand(entity, occupants, icons);
        }

        return cdo is not null && ItemNames.Read(cdo) is { } naming
            ? ItemVariants.Expand(
                entity, naming, Heroes, SetBonuses,
                PropertyWalk.GetString(cdo, SetBonusKeyField),
                DynamicArt(cdo, icons))
            : [entity];
    }

    private HeroArt DynamicArt(UObject cdo, IconWriter? icons)
    {
        var mappingKey = PropertyWalk.GetString(cdo, VisualKeyField);
        var slot = SlotNames.ByOrdinal(PropertyWalk.AsOrdinal(PropertyWalk.Get(cdo, SlotOrdinalField)?.GenericValue) ?? 0);
        var tiers = DynamicVisuals.Tiers(cdo);

        return codename =>
        [
            .. DynamicVisuals.Icons(mappingKey, codename, slot?.Type, tiers)
                .Select(entry => (entry.Rarity, Image: Written(entry.IconPath, icons)))
                .Where(entry => entry.Image is not null)
                .Select(entry => new RarityImage(entry.Rarity, entry.Image!)),
        ];
    }

    private Entity WithTableText(Entity entity, string tagName, IconWriter? icons)
    {
        if (SetBonuses.ForTag(tagName) is { } bonus)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? bonus.DisplayName,
                Description = entity.Description ?? bonus.Description,
                SetCount = bonus.SetCount,
                AllowedRoles = bonus.AllowedRoles,
                AllowedAttributes = bonus.AllowedAttributes,
                Augmentation = bonus.Augmentation,
                Icon = entity.Icon ?? Written(bonus.IconPath, icons),
            };
        }

        if (Traits.ForTag(tagName) is { } trait)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? trait.DisplayName,
                Description = entity.Description ?? trait.Description,
                TraitCategoryTag = trait.CategoryTag,
                TraitCategory = trait.Category,
                MaxRank = trait.MaxRank,
                RandomChance = trait.RandomChance,
                Augmentation = trait.Augmentation,
                RankRuns = trait.Runs,
                Icon = entity.Icon ?? Written(trait.IconPath, icons),
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
                Icon = entity.Icon ?? Written(size.IconPath, icons),
            };
        }

        if (TableNames.Find(tagName) is { } row)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? row.DisplayName,
                Description = entity.Description ?? row.Description,
                Rarity = entity.Rarity ?? row.Rarity,
                RarityTier = entity.RarityTier ?? row.RarityTier,
                Icon = entity.Icon ?? Written(row.IconPath, icons),
            };
        }

        return entity;
    }

    private static Entity WithGemNode(Entity entity, GemLadderNode node, IconWriter? icons) => entity with
    {
        DisplayName = node.DisplayName ?? entity.DisplayName,
        Description = node.Description ?? entity.Description,
        Placeholders = node.Placeholders ?? entity.Placeholders,
        GemType = node.TypeName,
        GemNode = node.Node,
        RequiredGemPower = node.RequiredPower,
        Replaces = node.Replaces,
        Icon = Written(node.IconPath, icons) ?? entity.Icon,
    };

    private static GemOverflow? WithOverflow(GemOverflow? overflow, IconWriter? icons) =>
        overflow is null ? null : overflow with { Icon = Written(overflow.Icon, icons) };

    private static string? Written(string? iconPath, IconWriter? icons) =>
        iconPath is null ? null : icons?.Invoke(iconPath);

    private Entity WithFixedItem(Entity entity, HeroItem item, IconWriter? icons) => entity with
    {
        DisplayName = entity.DisplayName ?? item.DisplayName,
        Slot = item.Slot ?? entity.Slot,
        ArmorType = item.ArmorType,
        Stats = entity.Stats ?? Empty(item.Stats),
        SetBonus = SetBonuses.ForRow(item.SetBonus),
        UniqueEquipped = item.UniqueEquipped ? true : null,
        Rarities = entity.Rarities ?? (item.Rarity is { } rung ? [rung] : null),
        Heroes = Empty([.. item.Heroes.Select(hero => Heroes.Shipped(hero) ?? hero)]),
        Icon = entity.Icon ?? (item.IconPath is { } path ? icons?.Invoke(path) : null),
    };

    private static Entity WithTalent(Entity entity, Talent talent, IconWriter? icons) => entity with
    {
        DisplayName = talent.DisplayName ?? entity.DisplayName,
        Description = talent.Description ?? entity.Description,
        Icon = (talent.IconPath is { } path ? icons?.Invoke(path) : null) ?? entity.Icon,
        Heroes = [talent.HeroName],
        TalentSlot = talent.Slot,
        TalentRow = talent.Row,
        TalentColumn = talent.Column,
        TalentCost = talent.Cost,
    };

    private Entity Body(
        UObject? cdo,
        string? assetName,
        Display display,
        AbilityPresentation? presentation,
        IconWriter? icons)
    {
        var tuning = TuningNamespace.For(assetName, _constants);
        var bindings = cdo is null ? [] : CurveBindings.Collect(cdo, _constants);

        var tokenHolder = (IPropertyHolder?)presentation?.Row ?? cdo;

        var placeholders = tokenHolder is null
            ? []
            : DescriptionTokens.Align(
                DescriptionTokens.Resolve(tokenHolder, _provider, _constants, _presets, tuning, assetName),
                display.Description);

        var split = Numbers.Split(
            bindings, tuning, UsesCharges(cdo), row => Charges.Supplies(row, display.Name));
        var scaling = new SortedSet<string>(StringComparer.Ordinal);

        var bands = new EffectSpreads();

        var ownBand = cdo is null ? null : _presets.Of(cdo);

        if (cdo is not null)
        {
            foreach (var attribute in GameplayEffects.Captures(cdo) ?? []) scaling.Add(attribute);

            foreach (var effect in Effects(cdo))
            {
                if (effect.Spread is { } band) bands.Add(effect.Constants ?? [], band);
                if (effect.Constants is null && effect.CapturedAttributes is null) continue;

                foreach (var attribute in effect.CapturedAttributes ?? []) scaling.Add(attribute);
                foreach (var binding in effect.Constants ?? []) Numbers.Add(split, binding, tuning);
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

        Supplement(split, tuning, UsesCharges(cdo));

        var grantedTags = cdo is null ? [] : PropertyWalk.GetTags(cdo, GrantedTagsField) ?? [];
        foreach (var tag in grantedTags) SupplementTalent(split, tag);

        var detail = display.Detail.Where(line => line != display.Description).ToArray();
        var (baseline, talents) = Numbers.SplitTalents(split.Rest);

        var reachable = Rejoined(baseline, claimed, split.Promoted);
        var taken = new HashSet<string>(claimed.Keys, StringComparer.Ordinal);

        foreach (var name in placeholders.Keys.ToArray())
        {
            var placeholder = placeholders[name];
            var own = placeholder.Constants is not null;
            var source = placeholder.Constants ?? reachable;

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
                    placeholders[name] = placeholder with
                    {
                        Attribute = resolved.Attribute,
                        Coefficient = resolved.Value,
                        Spread = Band(resolved.Key, tuning, bands, ownBand, placeholder.PresetSpread),
                    };
                    continue;
                }
            }
            else if (!own && Numbers.SoleField(reachable, split.Own, taken, wanted) is { } row)
            {
                taken.Add(row.Key);
                placeholders[name] = placeholder with { Value = row.Value, Tickrate = tickrate };
                continue;
            }

            if (placeholder.Constants is { } selected && Sole(selected) is { } value)
            {
                placeholders[name] = placeholder with { Value = value, Tickrate = tickrate };
            }
        }

        return new Entity
        {
            DisplayName = display.Name,
            Tags = cdo is null ? null : Empty(AbilityTypes(cdo)),
            Description = display.Description,
            Detail = Empty(detail),
            Icon = display.Icon is null ? null : icons?.Invoke(display.Icon),
            CastTime = Named(split, "castTime"),
            ChannelTime = Named(split, "channelTime"),
            ChannelTick = Named(split, "channelTick"),
            Cooldown = Named(split, "cooldown"),
            Charges = Named(split, "charges"),
            Cost = Named(split, "cost"),
            Resource = display.Resource,
            Range = Named(split, "range"),
            Radius = Named(split, "radius"),
            Duration = Named(split, "duration"),
            Period = Named(split, "period"),
            ScalesWithHaste = ScalesWithHaste(cdo) ? true : null,
            TickrateIgnoresHaste = cdo is not null && GameplayEffects.IgnoresHaste(cdo) ? true : null,
            ScalesOff = scaling.Count == 0 ? null : [.. scaling],
            Placeholders = placeholders.Count == 0 ? null : placeholders,
            Constants = baseline.Count == 0 ? null : baseline,
            Talents = talents.Count == 0
                ? null
                : talents.ToDictionary(t => t.Key, t => (IReadOnlyDictionary<string, object?>)t.Value, StringComparer.Ordinal),
            TalentGates = cdo is null ? null : Empty(TalentGates(cdo, talents.Keys)),
            GrantedTags = Empty(grantedTags),
        };
    }

    private double? Band(
        string coefficientKey, string? tuning, EffectSpreads bands, double? ownBand, double? tokenBand)
    {
        var spread = Spread.For(
            coefficientKey, tuning, _constants, bands, ownBand, tokenBand, out var overridden);

        if (spread is not null) _spreadSources.TryAdd(coefficientKey, overridden);
        return spread;
    }

    private static bool UsesCharges(UObject? cdo) =>
        cdo is not null &&
        PropertyWalk.GetString(cdo, ChargeEffectField) is { } effect &&
        effect.EndsWith(NativeChargeEffect, StringComparison.OrdinalIgnoreCase);

    private static bool ScalesWithHaste(UObject? cdo) =>
        cdo is not null && PropertyWalk.Get(cdo, HasteScalingField)?.GenericValue is true;

    private const string HasteScalingField = "ScaleTimeWithHaste";

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

    private void Supplement(SplitNumbers split, string? tuning, bool usesCharges)
    {
        if (tuning is null) return;

        foreach (var record in _constants.DirectlyUnder(tuning))
        {
            if (split.Rows.Contains(record.Key)) continue;

            var leaf = record.Key[(tuning.Length + 1)..];
            var binding = new CurveBinding(leaf, record.Key)
            {
                Value = record.Value,
                Values = record.Values is { Length: > 1 } ranks ? ranks : null,
                ConstantsTable = record.Table,
            };

            var one = Numbers.Split([binding], tuning, usesCharges);
            split.Rows.Add(record.Key);

            foreach (var (field, value) in one.Named)
            {
                if (!split.Named.TryAdd(field, value)) continue;

                NamespaceSupplied++;

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

    private static double? Named(SplitNumbers split, string field) =>
        split.Named.TryGetValue(field, out var value) ? value : null;

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

    private static double? Sole(IReadOnlyDictionary<string, object?> constants)
    {
        double? found = null;

        foreach (var value in constants.Values)
        {
            if (Flat(value) is not { } number) continue;
            if (found is not null) return null;
            found = number;
        }

        return found;
    }

    private static double? Flat(object? value) => value switch
    {
        double number => number,
        double?[] { Length: > 0 } ladder when ladder[0] is { } first && Array.TrueForAll(ladder, step => step == first) =>
            first,
        _ => null,
    };

    private static Entity FillFrom(Entity outer, IEnumerable<Entity> inner)
    {
        foreach (var ability in inner) outer = Fill(outer, ability);
        return outer;
    }

    private static Entity Fill(Entity outer, Entity inner) => outer with
    {
        DisplayName = outer.DisplayName ?? inner.DisplayName,
        Description = outer.Description ?? inner.Description,
        Detail = outer.Detail ?? inner.Detail,
        Icon = outer.Icon ?? inner.Icon,
        Tags = outer.Tags ?? inner.Tags,
        CastTime = outer.CastTime ?? inner.CastTime,
        ChannelTime = outer.ChannelTime ?? inner.ChannelTime,
        ChannelTick = outer.ChannelTick ?? inner.ChannelTick,
        Cooldown = outer.Cooldown ?? inner.Cooldown,
        Charges = outer.Charges ?? inner.Charges,
        Cost = outer.Cost ?? inner.Cost,
        Resource = outer.Resource ?? inner.Resource,
        Range = outer.Range ?? inner.Range,
        Radius = outer.Radius ?? inner.Radius,
        Duration = outer.Duration ?? inner.Duration,
        Period = outer.Period ?? inner.Period,
        ScalesWithHaste = outer.ScalesWithHaste ?? inner.ScalesWithHaste,
        ScalesOff = Union(outer.ScalesOff, inner.ScalesOff),
        Placeholders = Union(outer.Placeholders, inner.Placeholders),
        Constants = Union(outer.Constants, inner.Constants),
        Talents = UnionTalents(outer.Talents, inner.Talents),
        TalentGates = Union(outer.TalentGates, inner.TalentGates),
        GrantedTags = Union(outer.GrantedTags, inner.GrantedTags),
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

    private static IReadOnlyList<string>? Union(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
    {
        if (first is null || second is null) return first ?? second;
        return [.. first.Concat(second).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
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
        }

        variants.Sort((a, b) => string.Compare(a.HeroName ?? a.Hero, b.HeroName ?? b.Hero, StringComparison.OrdinalIgnoreCase));
        return (null, null);
    }

    private List<EffectRef> Effects(UObject asset) =>
        [.. GameplayEffects.ReferencedIn(asset, _effectNames)
            .Select(reference => GameplayEffects.Resolve(_provider, _constants, _presets, reference.Path))];

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

    private List<Entity> LegendaryStats(UObject cdo, IconWriter? icons) =>
        [.. PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => LoadAugmentation(path, icons))];

    private List<int> LegendaryStatIds(UObject cdo) =>
        [.. PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(_augmentationIds.For)
            .OfType<int>()];

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

        return Body(cdo, name, Display.From(presentation?.Row, cdo), presentation, icons);
    }

    private UObject? Load(string assetPath)
    {
        var package = AbilityPresentationIndex.PackagePath(assetPath);
        if (!_provider.TryLoadPackage(package, out var loaded)) return null;

        var exports = loaded.GetExports().ToArray();
        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }

    private FAssetData? FindAsset(string name)
    {
        if (_byPrimaryAssetName.TryGetValue(name, out var primary)) return primary;
        return _byAssetName.TryGetValue(name, out var byName) ? byName : null;
    }

    private void IndexAssets()
    {
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in _assets)
        {
            if (TooltipCommand.TagValue(asset, "PrimaryAssetName") is { } primary)
            {
                _byPrimaryAssetName.TryAdd(primary, asset);
            }

            var name = asset.AssetName.Text;
            if (ambiguous.Contains(name)) continue;
            if (_byAssetName.TryAdd(name, asset)) continue;

            _byAssetName.Remove(name);
            ambiguous.Add(name);
        }
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
}
