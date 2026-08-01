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
    private const string EffectTable = "GameplayEffect";
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";
    private const string AbilityTypePrefix = "AbilityType.";
    private const string TalentTagPrefix = "Talents.";
    private const string TagSuffix = "Tag";
    private const string GrantedTagsField = "GrantedGameplayTags";
    private const string ChargeEffectField = "ChargeGameplayEffectClass";
    private const string NativeChargeEffect = "CRChargeEffect";

    private readonly AbstractFileProvider _provider;
    private readonly FAssetData[] _assets;
    private readonly ConstantsTable _constants;
    private readonly List<TagRecord> _tags;
    private readonly Dictionary<string, FAssetData> _byPrimaryAssetName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FAssetData> _byAssetName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _effectNames = new(StringComparer.OrdinalIgnoreCase);

    public AbilityPresentationIndex Presentation { get; }
    public GrantedAbilityMappings Mappings { get; }
    public HeroNames Heroes { get; }
    public ItemSlots Slots { get; }

    /// <summary>How many numbers the tuning tables supplied that no asset names. Reported, not silent.</summary>
    public int NamespaceSupplied { get; private set; }

    public TooltipResolver(AbstractFileProvider provider)
    {
        _provider = provider;
        _assets = [.. RegistryCommand.ReadAssets(provider)];
        _tags = [.. CsvExportCommands.ReadTags(provider)];
        _constants = new ConstantsTable(provider);

        IndexAssets();

        // The game's own enumeration of its gameplay effects. Naming conventions do not identify
        // one - four assets named GE_ are AoE actors - and neither does the native parent class,
        // which is CRCostEffect or CRProcEffect or six others as often as it is GameplayEffect.
        foreach (var tag in _tags)
        {
            if (tag.Table.Equals(EffectTable, StringComparison.OrdinalIgnoreCase)) _effectNames.Add(tag.Name);
        }

        Presentation = new AbilityPresentationIndex(provider, _assets);
        Mappings = new GrantedAbilityMappings(provider, _assets);
        Heroes = new HeroNames(provider, _assets);
        Slots = new ItemSlots(provider, _assets);
    }

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
    /// One entity, whole. An item that reads differently per hero carries every one of its
    /// variants here rather than being asked for a hero at a time.
    /// </summary>
    public Entity Resolve(TagRecord tag, IconWriter? icons)
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

        return body with
        {
            Table = tag.Table,
            NetId = tag.NetId,
            Name = tag.Name,
            Type = cdo is null ? null : ItemTypeOf(cdo),
            Stats = cdo is null ? null : Empty(Slots.For(cdo)),
            Grants = granted,
            LegendaryStats = cdo is null ? null : Empty(LegendaryStats(cdo, icons)),
            Variants = Empty(variants),
        };
    }

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
                DescriptionTokens.Resolve(presentation.Row, _provider, _constants, tuning, assetName),
                display.Description);

        var split = Numbers.Split(bindings, tuning, UsesCharges(cdo));
        var scaling = new SortedSet<string>(StringComparer.Ordinal);

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
                if (effect.Constants is null && effect.CapturedAttributes is null) continue;

                foreach (var attribute in effect.CapturedAttributes ?? []) scaling.Add(attribute);
                foreach (var binding in effect.Constants ?? []) Numbers.Add(split, binding, tuning);
            }
        }

        foreach (var placeholder in placeholders.Values)
        {
            foreach (var attribute in placeholder.ScalesOff ?? []) scaling.Add(attribute);

            // A number the description already accounts for is not repeated in the constants.
            foreach (var key in placeholder.Constants?.Keys ?? []) split.Rest.Remove(key);
        }

        // Rows the tuning tables place under this entity that no property names. Native code reads
        // an ability's cost that way, so the table is the only record that the cost is the
        // ability's at all.
        Supplement(split, tuning);

        // A talent asset reaches almost none of its own numbers: they sit under the ability it
        // modifies rather than under anything named after the talent, and the tag it grants is the
        // only link. Without this Glacial Assault exports without the 40% its own text quotes.
        var grantedTags = cdo is null ? [] : PropertyWalk.GetTags(cdo, GrantedTagsField) ?? [];
        foreach (var tag in grantedTags) SupplementTalent(split, tag);

        var detail = display.Detail.Where(line => line != display.Description).ToArray();
        var (baseline, talents) = Numbers.SplitTalents(split.Rest);

        // The number a token prints is a coefficient times a stat, so a token that carries neither
        // reconstructs nothing. A token drawing on an effect finds it among that effect's rows; a
        // token that names its own ability has to find it among the ability's.
        foreach (var name in placeholders.Keys.ToArray())
        {
            var placeholder = placeholders[name];
            var source = placeholder.Constants ?? baseline;

            if (Coefficients.From(source) is not { } resolved) continue;
            placeholders[name] = placeholder with { Attribute = resolved.Attribute, Coefficient = resolved.Value };
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
    /// </summary>
    private void Supplement(SplitNumbers split, string? tuning)
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

            var one = Numbers.Split([binding], tuning);
            split.Rows.Add(record.Key);

            foreach (var (field, value) in one.Named)
            {
                if (split.Named.TryAdd(field, value)) NamespaceSupplied++;
            }

            foreach (var (key, value) in one.Rest)
            {
                if (split.Rest.TryAdd(key, value)) NamespaceSupplied++;
            }
        }
    }

    private static double? Named(SplitNumbers split, string field) =>
        split.Named.TryGetValue(field, out var value) ? value : null;

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
    /// The gameplay effects an asset applies, with the attributes they scale off. An ability holds
    /// its damage effect as an ordinary class reference - DamageEffect, DamageEffectRef, DotEffect
    /// and a dozen other names designers chose - so the reference is recognised by its target being
    /// a known effect rather than by the field it sits in.
    /// </summary>
    private List<EffectRef> Effects(UObject asset)
    {
        var found = new List<EffectRef>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in PropertyWalk.Walk(asset))
        {
            if (PropertyWalk.AsString(property.Value) is not { } path) continue;

            // A native class shares its name with the effect tag but has no package to read, so a
            // "/Script/..." reference is a class the effect derives from, not an effect.
            if (!path.Contains('/') || path.StartsWith(ScriptRoot, StringComparison.OrdinalIgnoreCase)) continue;

            var name = AbilityPresentationIndex.AssetName(path);
            if (name.EndsWith(ClassSuffix, StringComparison.Ordinal)) name = name[..^ClassSuffix.Length];

            if (!_effectNames.Contains(name)) continue;
            if (!seen.Add(path)) continue;

            found.Add(GameplayEffects.Resolve(_provider, _constants, path));
        }

        return found;
    }

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
    /// The line under an item's name. A dynamic item's HeroItemType is a byte enum, which cooks
    /// down to an ordinal, but the rule set it names is a gameplay tag and spells the type out.
    /// </summary>
    private static string? ItemTypeOf(UObject cdo) =>
        PropertyWalk.GetString(cdo, RuleSetField) is { } ruleSet ? HeroNames.LastSegment(ruleSet) : null;

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
