using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>Turns an icon's object path into a written image file. The caller decides where.</summary>
public delegate string? IconWriter(string iconObjectPath);

/// <summary>
/// Resolves a named entity to what its tooltip shows, and no more.
///
/// The chain is the same for any table. A gameplay tag names an asset; the asset's class default
/// object holds FScalableFloat references; each of those names a curve-table row; and that row
/// name is a key into the shipped tuning constants. Effects the asset applies are read the same
/// way and folded in, because an ability's damage is never on the ability.
///
/// The shared indexes are built once and reused, so resolving one entity and resolving the whole
/// game cost the same setup.
/// </summary>
public sealed class TooltipResolver
{
    private const string MappingKeyField = "GrantedAbilityMappingKey";
    private const string RuleSetField = "RuleSet";
    private const string RuleSetPrefix = "DynamicHeroItemRuleSet.";
    private const string SetBonusKeyField = "SetBonusMappingKey";
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

    /// <summary>
    /// Where each coefficient row's band came from, keyed by the row so the count is over shipped
    /// tuning rows rather than over how many times the export happened to resolve one. True is an
    /// ability's own Spread row, false the calculation preset behind its effect.
    /// </summary>
    private readonly Dictionary<string, bool> _spreadSources = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rows two effects claim under different bands, which answer with no band at all.</summary>
    private readonly HashSet<string> _contestedBands = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<TagRecord> _tags;
    private readonly Dictionary<string, FAssetData> _byPrimaryAssetName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FAssetData> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _effectNames = new(StringComparer.OrdinalIgnoreCase);

    public AbilityPresentationIndex Presentation { get; }
    public EffectIconSources EffectIcons { get; }
    public GrantedAbilityMappings Mappings { get; }
    public HeroNames Heroes { get; }
    public ItemSlots Slots { get; }
    public StatCalculation StatCalculation { get; }
    public ItemSlotNames SlotNames { get; }
    public ArmorTypes Armor { get; }
    public HeroItemTable HeroItems { get; }
    public GrantedCharges Charges { get; }
    public ItemSetBonuses SetBonuses { get; }
    public ItemTableNames TableNames { get; }
    public DynamicItemRuleSets RuleSets { get; }
    public TalentTrees Talents { get; }
    public GemDefinitions Gems { get; }
    public ItemTraits Traits { get; }
    public Vocabulary Vocabulary { get; }

    /// <summary>How many numbers the tuning tables supplied that no asset names. Reported, not silent.</summary>
    public int NamespaceSupplied { get; private set; }

    /// <summary>How many entities took their icon from the entity that applies them.</summary>
    public int IconsInherited { get; private set; }

    /// <summary>
    /// How many entities were left without an icon because the entities that apply them ship
    /// different art. Reported apart from the ones nothing applies, because a contested pairing is
    /// a decision waiting to be made and an unapplied effect is art the build does not contain.
    /// </summary>
    public int IconsContested { get; private set; }

    /// <summary>How many calculation preset rows the build ships.</summary>
    public int PresetCount => _presets.Count;

    /// <summary>Coefficient rows whose band came from an ability's own Spread row.</summary>
    public int SpreadOverrides => _spreadSources.Values.Count(fromRow => fromRow);

    /// <summary>Coefficient rows whose band came from the preset behind their effect.</summary>
    public int SpreadPresetDefaults => _spreadSources.Values.Count(fromRow => !fromRow);

    /// <summary>
    /// Rows two effects bind under different bands, which therefore export none. Named rather than
    /// counted, because one is a shipped disagreement worth looking at and a hundred would mean the
    /// join is wrong.
    /// </summary>
    public IReadOnlyCollection<string> SpreadContested => _contestedBands;

    public TooltipResolver(AbstractFileProvider provider)
    {
        _provider = provider;
        _assets = [.. RegistryCommand.ReadAssets(provider)];
        _tags = [.. CsvExportCommands.ReadTags(provider)];
        _constants = new ConstantsTable(provider);
        _presets = new EffectCalculationPresets(provider, _assets);

        IndexAssets();

        // The game's own enumeration of its gameplay effects. Naming conventions do not identify
        // one - four assets named GE_ are AoE actors - and neither does the native parent class,
        // which is CRCostEffect or CRProcEffect or six others as often as it is GameplayEffect.
        foreach (var tag in _tags)
        {
            if (tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) _effectNames.Add(tag.Name);
        }

        var augmentations = new AugmentationIds(_tags);
        var rarities = new RarityNames(provider, _assets);

        Presentation = new AbilityPresentationIndex(provider, _assets);
        Mappings = new GrantedAbilityMappings(provider, _assets);
        Slots = new ItemSlots(provider, _assets);
        SlotNames = new ItemSlotNames(provider, _assets);
        Armor = new ArmorTypes(provider, _assets);
        SetBonuses = new ItemSetBonuses(provider, _assets, augmentations);
        TableNames = new ItemTableNames(provider, _assets);
        RuleSets = new DynamicItemRuleSets(provider, _assets);
        Gems = new GemDefinitions(provider, _assets, _constants, augmentations, rarities);
        Traits = new ItemTraits(
            provider, _assets, _constants, augmentations, new TraitCategoryNames(provider, _assets));

        // Read after the slot and armour vocabularies, because the two weight tables it publishes
        // are keyed by ordinal in the build and by the printed name in the export.
        StatCalculation = new StatCalculation(provider, _assets, SlotNames, Armor);

        // The fixed item table is read before the heroes are, because the weight templates its
        // rows name are the only shipped source that says what each hero's primary stat is.
        HeroItems = new HeroItemTable(provider, _assets, Armor, SlotNames);
        Heroes = new HeroNames(provider, _assets, Armor, HeroItems.PrimaryStatByHero);

        Charges = new GrantedCharges(provider, _assets, Presentation);

        // Read after the heroes, because a tree is one hero's and says so only through the asset
        // name it is filed under.
        Talents = new TalentTrees(provider, _assets, _tags, Heroes);

        Vocabulary = new Vocabulary(provider, _assets, Heroes, rarities, StatCalculation, Slots.RandomStatPool);

        // Last, because it walks the class default object of every tag that reaches one and so
        // needs the asset join and the presentation rows already standing. Building it here rather
        // than recording the edges as entities are resolved is what makes an inherited icon
        // independent of the order tags are asked for: one entity resolved on its own inherits
        // exactly what it inherits in the bulk export.
        EffectIcons = new EffectIconSources(
            _tags, _effectNames, Presentation, tag => LoadDefaultObject(FindAsset(tag.Name)));
    }

    /// <summary>A rule set under the name the rarity table is keyed by, without its tag prefix.</summary>
    private static string ShortRuleSet(string tagName) =>
        tagName.StartsWith(RuleSetPrefix, StringComparison.OrdinalIgnoreCase)
            ? tagName[RuleSetPrefix.Length..]
            : tagName;

    public int ConstantCount => _constants.Count;

    public IReadOnlyList<TagRecord> Tags => _tags;

    /// <summary>The tag with this id or name in the given table.</summary>
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

    /// <summary>
    /// How much of a table the presentation layer covers. Abilities that are internal - passives,
    /// targeting helpers, talent monitors - have no row and never show a tooltip, so a shortfall
    /// is expected rather than a failure.
    /// </summary>
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

    /// <summary>
    /// One entity, as the entries a player would recognise. Most tags are one entry; an item that
    /// reads as a different named thing per armour type or per hero is every one of those things,
    /// each carrying the stats and the granted ability that reading actually has.
    /// </summary>
    public IReadOnlyList<Entity> Resolve(TagRecord tag, IconWriter? icons)
    {
        var asset = FindAsset(tag.Name);
        var cdo = LoadDefaultObject(asset);
        var presentation = Presentation.Find(tag.Name);
        var display = Display.From(presentation?.Row, cdo);

        var variants = cdo is null ? [] : HeroVariants.Collect(cdo, Heroes);

        // An item that names the heroes it is for is only ever those heroes, however many the
        // mapping tables answer for.
        var restricted = variants.Count > 0;

        // An augmentation asked for in its own right carries no text and names gameplay abilities,
        // exactly as it does when an item reaches it. Following them here as well means the answer
        // does not depend on which end of the chain the query started from.
        var body = Body(cdo, asset?.AssetName.Text ?? tag.Name, display, presentation, icons);
        if (cdo is not null) body = FillFrom(body, GrantedAbilities(cdo, icons));

        var granted = ResolveGranted(cdo, variants, restricted, icons);

        var ruleSet = cdo is null ? null : PropertyWalk.GetString(cdo, RuleSetField);

        var entity = body with
        {
            Table = tag.Table,
            NetId = tag.NetId,
            Name = tag.Name,
            Slot = SlotNames.FromTag(tag.Name)?.Shipped,
            GemSlot = RuleSets.HasGemSocket(ruleSet) ? true : null,
            RuleSet = ruleSet is null ? null : ShortRuleSet(ruleSet),
            Stats = cdo is null ? null : Empty(Slots.For(cdo, StatCalculation)),
            Grants = granted,
            LegendaryStats = cdo is null ? null : Empty(LegendaryStats(cdo, icons)),
            Variants = Empty(variants),
        };

        entity = WithTableText(entity, tag.Name, icons);

        if (HeroItems.Find(tag.Name) is { } fixedItem) entity = WithFixedItem(entity, fixedItem, icons);

        if (tag.Table.Equals(TalentTrees.AugmentationTable, StringComparison.OrdinalIgnoreCase))
        {
            if (Talents.ForAugmentation(tag.Name) is { } talent) entity = WithTalent(entity, talent, icons);
            if (Gems.ForAugmentation(tag.Name) is { } node) entity = WithGemNode(entity, node, icons);
        }

        // A gameplay effect that shows a tooltip and ships no art takes the art of the entity that
        // applies it, where exactly one icon-carrying entity does. Placed here, after every step
        // that can set an icon of the entity's own and before the two expansions that copy the
        // record, so it only ever fills a gap and every line an expansion produces agrees.
        //
        // Gated on the name as well as the icon. 1,299 of the 3,260 iconless effects have a
        // determined source and only 197 of those are named: the rest are cooldowns, costs and
        // charge machinery that export as a bare id, where an icon would sit beside nothing a
        // reader could see.
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
                entity, naming, Heroes,
                SetBonuses, cdo is null ? null : PropertyWalk.GetString(cdo, SetBonusKeyField))
            : [entity];
    }

    /// <summary>
    /// Text a shipped table holds for a tag that reaches no asset at all. Set bonuses, item traits,
    /// currencies and gem types are data tables and nothing else, so without this they export as a
    /// bare id however much a tooltip would show for them.
    /// </summary>
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
                Icon = entity.Icon ?? Written(size.IconPath, icons),
            };
        }

        if (TableNames.Find(tagName) is { } row)
        {
            return entity with
            {
                DisplayName = entity.DisplayName ?? row.DisplayName,
                Description = entity.Description ?? row.Description,
                Icon = entity.Icon ?? Written(row.IconPath, icons),
            };
        }

        return entity;
    }

    /// <summary>
    /// A gem ladder node on the augmentation that grants it. The augmentation is where the node's
    /// mechanics already are, and the table is where its name, its threshold and the sentence a
    /// player reads are, so the two only make one tooltip together.
    /// </summary>
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

    /// <summary>
    /// A tag that names a row of the fixed item table rather than an asset. Nothing else in the
    /// build defines these 480 items, so without this they export as a bare id and a tag name.
    /// </summary>
    private Entity WithFixedItem(Entity entity, HeroItem item, IconWriter? icons) => entity with
    {
        DisplayName = entity.DisplayName ?? item.DisplayName,
        Slot = item.Slot ?? entity.Slot,
        ArmorType = item.ArmorType,
        Stats = entity.Stats ?? Empty(item.Stats),
        SetBonus = SetBonuses.ForRow(item.SetBonus),
        UniqueEquipped = item.UniqueEquipped ? true : null,
        Heroes = Empty([.. item.Heroes.Select(hero => Heroes.Shipped(hero) ?? hero)]),
        Icon = entity.Icon ?? (item.IconPath is { } path ? icons?.Invoke(path) : null),
    };

    /// <summary>
    /// A talent as the tree that places it states it. This is the one text path in the file that
    /// runs the other way round: CRTalentData overrides whatever the augmentation and the ability
    /// fallthrough produced instead of filling in behind it.
    ///
    /// It has to. An augmentation carrying no text of its own falls through GrantedAbilities to the
    /// ability it modifies, so Slow Burn exports as "Fire Ball" with Reign of Fire's art. Where the
    /// augmentation does carry text it is a stale draft: 7 of the 215 disagree with the tree on the
    /// name and all 12 that carry a description disagree on the numbers.
    /// </summary>
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

    /// <summary>
    /// Everything an asset itself contributes to a tooltip: the text, the icon, the numbers a
    /// reader expects in a fixed place, and the tuning behind each substitution in the text.
    /// </summary>
    private Entity Body(
        UObject? cdo,
        string? assetName,
        Display display,
        AbilityPresentation? presentation,
        IconWriter? icons)
    {
        var tuning = TuningNamespace.For(assetName, _constants);
        var bindings = cdo is null ? [] : CurveBindings.Collect(cdo, _constants);

        var placeholders = presentation is null
            ? []
            : DescriptionTokens.Align(
                DescriptionTokens.Resolve(presentation.Row, _provider, _constants, _presets, tuning, assetName),
                display.Description);

        var split = Numbers.Split(
            bindings, tuning, UsesCharges(cdo), row => Charges.Supplies(row, display.Name));
        var scaling = new SortedSet<string>(StringComparer.Ordinal);

        // Which of the entity's numbers roll in which band. An effect names its calculation preset
        // and binds the coefficient rows it multiplies, so the row is the join between the two.
        var bands = new EffectSpreads();

        // An entity that is itself a gameplay effect names its preset on its own class default
        // object, where the walk over referenced effects never looks.
        var ownBand = cdo is null ? null : _presets.Of(cdo);

        // The effects the asset applies, folded in. Their coefficients are the only record of what
        // an ability's damage scales off, and the effect itself is a separate entity in its own
        // right, so the reference to it adds nothing that is not already here or there.
        if (cdo is not null)
        {
            // An effect asked for in its own right captures its attributes on itself, so reading
            // only the effects it references would leave the effect tables with no scaling at all.
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

            // A number the description already accounts for is not repeated in the constants.
            foreach (var key in placeholder.Constants?.Keys ?? [])
            {
                if (split.Rest.Remove(key, out var value)) claimed[key] = value;
            }
        }

        // Rows the tuning tables place under this entity that no property names. Native code reads
        // an ability's cost that way, so the table is the only record that the cost is the
        // ability's at all.
        Supplement(split, tuning, UsesCharges(cdo));

        // A talent asset reaches almost none of its own numbers: they sit under the ability it
        // modifies rather than under anything named after the talent, and the tag it grants is the
        // only link. Without this Glacial Assault exports without the 40% its own text quotes.
        var grantedTags = cdo is null ? [] : PropertyWalk.GetTags(cdo, GrantedTagsField) ?? [];
        foreach (var tag in grantedTags) SupplementTalent(split, tag);

        var detail = display.Detail.Where(line => line != display.Description).ToArray();
        var (baseline, talents) = Numbers.SplitTalents(split.Rest);

        // The number a token prints is a coefficient times a stat, or a plain scalar where the
        // token's Calculation named a period or a duration. A token drawing on an effect finds
        // either among the rows that Calculation selected; a token that names its own ability has
        // to find its answer among the ability's, where a lone row means nothing.
        //
        // That search runs over every row the entity reaches, the rows other tokens account for
        // among them, because those are rivals even though they are not this token's answer. A row
        // a token does take stops being available to the next one: Luminous Barrier's absorb and
        // its damage reduction are two sentences and one coefficient, and printing that coefficient
        // twice says the barrier reduces damage by twelve times the wearer's Intellect.
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

            // A period is marked as one wherever it is answered, because a reader deciding whether a
            // haste value applies needs the quantity and not just the number.
            var tickrate = placeholder.Quantity == TokenQuantity.Period ? true : (bool?)null;

            // A token asking for a period or a duration is not asking for a coefficient, and taking
            // one puts the ability's damage where its clock should be.
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

    /// <summary>
    /// The band a coefficient rolls in, recording which layer answered so the export can report the
    /// split. Both fallbacks are wanted: the effect the token itself names is not always one the
    /// ability references directly, and an entity that is a gameplay effect names its own.
    /// </summary>
    private double? Band(
        string coefficientKey, string? tuning, EffectSpreads bands, double? ownBand, double? tokenBand)
    {
        var spread = Spread.For(
            coefficientKey, tuning, _constants, bands, ownBand, tokenBand, out var overridden);

        if (spread is not null) _spreadSources.TryAdd(coefficientKey, overridden);
        return spread;
    }

    /// <summary>
    /// Whether an ability actually runs on charges. A charge count in the tuning tables does not
    /// mean the ability has charges - Apocalypse carries one and has a single cast - and neither a
    /// gate tag nor the row's name tells the two apart. What does is the effect the ability charges
    /// through: the native CRChargeEffect is the charge machinery, while the shared cooldown
    /// effects that read as charge-shaped are about how the cooldown behaves.
    /// </summary>
    private static bool UsesCharges(UObject? cdo) =>
        cdo is not null &&
        PropertyWalk.GetString(cdo, ChargeEffectField) is { } effect &&
        effect.EndsWith(NativeChargeEffect, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the ability declares that its own times run with haste. This is read rather than
    /// inferred: CRGameplayAbility carries ScaleTimeWithHaste beside ScaleTimeWithAttackSpeed, and
    /// unversioned properties omit a flag left at its default, so an ability that says nothing is
    /// not making the claim.
    /// </summary>
    private static bool ScalesWithHaste(UObject? cdo) =>
        cdo is not null && PropertyWalk.Get(cdo, HasteScalingField)?.GenericValue is true;

    private const string HasteScalingField = "ScaleTimeWithHaste";

    /// <summary>
    /// The condition a talent group actually declares. An ability that gates something on a talent
    /// says so with a tag property beside the value - TalentPlacedProjectileAoeHasChargesTag next
    /// to TalentPlacedProjectileAoeHasChargesNumChargesRef - and the tag's last segment is the
    /// talent's name, which is the join. Only the declared ones are reported: a row merely named
    /// for a talent is not evidence of a condition, and 691 entities have such rows against 42
    /// that declare a gate.
    /// </summary>
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

    /// <summary>
    /// Everything the tuning tables hold for one talent, found through the tag it grants. Unlike an
    /// ability's namespace this one is taken to the bottom: nothing else nests inside a talent, so
    /// every row below it is the talent's own.
    /// </summary>
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

    /// <summary>
    /// The tuning rows sitting directly under an entity's namespace that nothing in the asset
    /// references. A cost effect is a native class with an empty default object, so an ability's
    /// cost is reachable no other way.
    ///
    /// A charge count arriving this way has to clear the same gate a referenced one does. A row
    /// nothing in the asset names is the weakest evidence in the build, and reading it as a charge
    /// count made Brimborn's stack cap of 20 an ability with twenty charges.
    /// </summary>
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

                // The row behind the field stays reachable by name, the same as one the asset
                // named. one holds a single binding, so this is that row.
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

    /// <summary>
    /// An entity's baseline constants with the rows its tokens claimed and the rows its named
    /// fields took put back, grouped the same way, so a row that names a talent stays out of the
    /// baseline however it got here.
    ///
    /// This is the set a token searches, not the set the export writes. A field's row is put back
    /// because a token can name the same number the field does; the constants the export writes
    /// come from the baseline, where the row stays out and does not repeat the field.
    /// </summary>
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

    /// <summary>
    /// The one number a token's selected rows hold, or null when they hold none or several. A row
    /// that scales with rank is four numbers and is not one of them.
    /// </summary>
    private static double? Sole(IReadOnlyDictionary<string, object?> constants)
    {
        double? found = null;

        foreach (var value in constants.Values)
        {
            if (value is not double number) continue;
            if (found is not null) return null;
            found = number;
        }

        return found;
    }

    /// <summary>
    /// A wrapper asset read as one tooltip with everything it grants. A talent grants several
    /// abilities and each carries its own tuning, so folding only the one that happens to be named
    /// would drop the rest. Text comes from the first that has any; the numbers come from all.
    /// </summary>
    private static Entity FillFrom(Entity outer, IEnumerable<Entity> inner)
    {
        foreach (var ability in inner) outer = Fill(outer, ability);
        return outer;
    }

    /// <summary>
    /// A wrapper asset and the ability it exists to grant, read as one tooltip. Text and numbers
    /// the wrapper does not carry come from the ability. The tuning is merged rather than chosen
    /// between: a wrapper that shares the ability's namespace reaches some of the same rows, and
    /// taking either side whole would drop what only the other reached.
    /// </summary>
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

    /// <summary>
    /// Talents merged field by field. Merging at the talent's name instead would let a wrapper that
    /// reached one of a talent's fields shadow the ability that reached all five.
    /// </summary>
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

    /// <summary>
    /// The ability an item grants. The item names only a mapping key; the per-hero tables say
    /// which ability that key answers for each hero. A relic answers the same ability for every
    /// hero and belongs on the item, a legendary answers a different one per hero and belongs on
    /// the variants, and the tables are what tells the two apart.
    /// </summary>
    private Entity? ResolveGranted(UObject? cdo, List<Variant> variants, bool restricted, IconWriter? icons)
    {
        if (cdo is null) return null;
        if (PropertyWalk.GetString(cdo, MappingKeyField) is not { } key) return null;

        var matches = Mappings.FindAll(key);
        if (matches.Count == 0) return null;

        var distinct = matches.Select(m => m.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinct.Length == 1) return LoadAugmentation(distinct[0], icons);

        // Different abilities per hero. An item that names no heroes of its own is for all of them
        // and gets a variant per mapping row, because the tables are the only place that
        // difference is recorded.
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
        return null;
    }

    /// <summary>
    /// The gameplay effects an asset applies, with the attributes they scale off. Which references
    /// count is <see cref="GameplayEffects.ReferencedIn"/>'s to say, because the same predicate
    /// inverted is what lets an effect inherit an icon from the entity that applies it.
    /// </summary>
    private List<EffectRef> Effects(UObject asset) =>
        [.. GameplayEffects.ReferencedIn(asset, _effectNames)
            .Select(reference => GameplayEffects.Resolve(_provider, _constants, _presets, reference.Path))];

    /// <summary>What kind of ability this is, as the tooltip labels it: Aoe, Casted, Magic.Fire, Major.</summary>
    private static List<string> AbilityTypes(UObject cdo) =>
        [.. (PropertyWalk.GetTags(cdo, "AbilityTags") ?? [])
            .Where(t => t.StartsWith(AbilityTypePrefix, StringComparison.Ordinal))
            .Select(t => t[AbilityTypePrefix.Length..])
            .Order(StringComparer.Ordinal)];

    /// <summary>The abilities an asset grants directly, each resolved in full.</summary>
    private List<Entity> GrantedAbilities(UObject asset, IconWriter? icons) =>
        [.. PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith("GrantedAbilities[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => ResolveAbility(path, icons))];

    /// <summary>A legendary's bonus stats, each its own augmentation asset.</summary>
    private List<Entity> LegendaryStats(UObject cdo, IconWriter? icons) =>
        [.. PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => LoadAugmentation(path, icons))];

    /// <summary>
    /// A character augmentation and, when it carries no text of its own, the ability it grants.
    /// An item's granted ability is usually a bare wrapper around a gameplay ability, so stopping
    /// at the augmentation returns an empty result for the tooltips with the most to show.
    /// </summary>
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

    /// <summary>One gameplay ability, resolved the same way a top-level one is.</summary>
    public Entity ResolveAbility(string assetPath, IconWriter? icons)
    {
        if (Load(assetPath) is not { } cdo) return new Entity();

        var name = AbilityPresentationIndex.AssetName(assetPath);
        var presentation = Presentation.Find(name);

        return Body(cdo, name, Display.From(presentation?.Row, cdo), presentation, icons);
    }

    /// <summary>The class default object of a referenced asset, or null when it will not load.</summary>
    private UObject? Load(string assetPath)
    {
        var package = AbilityPresentationIndex.PackagePath(assetPath);
        if (!_provider.TryLoadPackage(package, out var loaded)) return null;

        var exports = loaded.GetExports().ToArray();
        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }

    /// <summary>
    /// The same join the index command uses: data assets advertise their tag as PrimaryAssetName,
    /// ability and effect blueprints carry it as their asset name. Indexed rather than scanned,
    /// because the bulk export resolves every tag against every asset otherwise.
    /// </summary>
    private FAssetData? FindAsset(string name)
    {
        if (_byPrimaryAssetName.TryGetValue(name, out var primary)) return primary;
        return _byAssetName.TryGetValue(name, out var byName) ? byName : null;
    }

    /// <summary>
    /// An asset name shared by more than one package names nothing in particular, so it is dropped
    /// rather than resolved to whichever package happened to come first.
    /// </summary>
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

        // A blueprint's tuning lives on the class default object; a data asset is its own export.
        return exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault(e => e.Name.Equals(asset.AssetName.Text, StringComparison.OrdinalIgnoreCase))
            ?? exports.FirstOrDefault();
    }

    private static IReadOnlyList<T>? Empty<T>(IReadOnlyList<T> items) => items.Count == 0 ? null : items;
}
