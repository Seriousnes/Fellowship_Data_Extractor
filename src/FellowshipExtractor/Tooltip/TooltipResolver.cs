using System.Text.Json;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;

/// <summary>Turns an icon's object path into a written image file. The caller decides where.</summary>
public delegate TextureExport? IconWriter(string iconObjectPath, string nameHint);

/// <summary>The package the entity was read from.</summary>
public sealed record AssetRef(string Path, string Class, string? NativeClass);

/// <summary>Which presentation row supplied the text, and how the lookup found it.</summary>
public sealed record PresentationRef(string Table, string Row, string? MatchedBy);

/// <summary>
/// Everything the game shows for one named entity, and the whole property set it was read from.
/// The interpreted fields are what a tooltip renders; the property set is there so nothing is
/// lost to a field this code does not yet know to look at.
/// </summary>
public sealed record TooltipRecord(string Table, int NetId, string Name)
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? ItemType { get; init; }
    public string? Icon { get; init; }
    public TextureExport? IconImage { get; init; }
    public AssetRef? Asset { get; init; }
    public IReadOnlyList<ItemSlot>? Slots { get; init; }

    /// <summary>Abilities the entity grants directly, resolved in full.</summary>
    public IReadOnlyList<AbilityDetails>? Abilities { get; init; }

    /// <summary>Attributes this entity itself captures. Set on a gameplay effect.</summary>
    public IReadOnlyList<string>? CapturedAttributes { get; init; }

    /// <summary>Effects this entity applies, each with the attributes it scales off.</summary>
    public IReadOnlyList<EffectRef>? Effects { get; init; }

    public Augmentation? GrantedAbility { get; init; }
    public IReadOnlyList<HeroVariant>? Variants { get; init; }
    public IReadOnlyList<Augmentation>? LegendaryStats { get; init; }
    public PresentationRef? PresentationRow { get; init; }
    public string? DefaultObject { get; init; }
    public Dictionary<string, double>? Headline { get; init; }
    public Targeting? Targeting { get; init; }
    public IReadOnlyList<ResolvedToken>? DescriptionTokens { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }
    public JsonElement? Properties { get; init; }
}

/// <summary>
/// Resolves a named entity to everything needed to reconstruct its tooltip.
///
/// The chain is the same for any table. A gameplay tag names an asset; the asset's class default
/// object holds FScalableFloat references; each of those names a curve-table row; and that row
/// name is a key into the shipped tuning constants. Description tokens point at the gameplay
/// effect that produces the number, whose modifiers resolve through the same chain.
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
    public TooltipRecord Resolve(TagRecord tag, IconWriter? icons)
    {
        var asset = FindAsset(tag.Name);
        var cdo = LoadDefaultObject(asset);
        var presentation = Presentation.Find(tag.Name);

        var display = Display.From(presentation?.Row, cdo);
        var variants = cdo is null ? [] : HeroVariants.Collect(cdo, Heroes);

        // An item that names the heroes it is for is only ever those heroes, however many the
        // mapping tables answer for.
        var restricted = variants.Count > 0;

        // The entity's own icon is exported before anything it references, so that a shared texture
        // is filed under the entity being asked about rather than under whatever reached it first.
        TextureExport? icon = display.Icon is null ? null : icons?.Invoke(display.Icon, $"{tag.Table}-{tag.NetId}");

        // An augmentation asked for in its own right carries no text and names a gameplay ability,
        // exactly as it does when an item reaches it. Following it here as well means the answer
        // does not depend on which end of the chain the query started from.
        var abilities = cdo is null ? [] : GrantedAbilities(cdo, icons);
        var granting = abilities.FirstOrDefault(a => a.DisplayName is not null);

        display = display with
        {
            Name = display.Name ?? granting?.DisplayName,
            Description = display.Description ?? granting?.Description,
            Icon = display.Icon ?? granting?.Icon,
        };
        icon ??= granting?.IconImage;

        var granted = ResolveGranted(cdo, variants, restricted, icons);
        var legendary = cdo is null ? [] : LegendaryStats(cdo, icons);
        var bindings = cdo is null ? [] : CurveBindings.Collect(cdo, _constants);

        // A shared item's own DisplayName is an internal placeholder and each variant carries the
        // name the game actually shows, so the placeholder stays where it is rather than one
        // variant's name standing in for all of them.
        return new TooltipRecord(tag.Table, tag.NetId, tag.Name)
        {
            DisplayName = display.Name,
            Description = display.Description,
            Detail = display.Detail.Count == 0 ? null : display.Detail,
            ItemType = cdo is null ? null : ItemTypeOf(cdo),
            Icon = display.Icon,
            IconImage = icon,
            Asset = asset is null ? null : new AssetRef(
                asset.ObjectPath,
                asset.AssetClass.Text,
                TooltipCommand.TagValue(asset, "NativeParentClass") ?? TooltipCommand.TagValue(asset, "NativeClass")),
            Slots = cdo is null ? null : Empty(Slots.For(cdo)),
            Abilities = Empty(abilities),
            CapturedAttributes = cdo is null ? null : GameplayEffects.Captures(cdo),
            Effects = cdo is null ? null : Empty(Effects(cdo)),
            GrantedAbility = granted,
            Variants = Empty(variants),
            LegendaryStats = Empty(legendary),
            PresentationRow = presentation is null
                ? null
                : new PresentationRef(presentation.Table, presentation.RowName, presentation.MatchedBy),
            DefaultObject = cdo?.Name,
            Headline = Headline.From(bindings),
            Targeting = cdo is null ? null : Targeting.From(cdo),
            DescriptionTokens = presentation is null
                ? null
                : Empty(DescriptionTokens.Resolve(presentation.Row, _provider, _constants)),
            Constants = Empty(bindings),
            Properties = cdo is null ? null : UeJson.ToElement(cdo),
        };
    }

    /// <summary>
    /// The ability an item grants. The item names only a mapping key; the per-hero tables say
    /// which ability that key answers for each hero. A relic answers the same ability for every
    /// hero and belongs on the item, a legendary answers a different one per hero and belongs on
    /// the variants, and the tables are what tells the two apart.
    /// </summary>
    private Augmentation? ResolveGranted(
        UObject? cdo, List<HeroVariant> variants, bool restricted, IconWriter? icons)
    {
        if (cdo is null) return null;
        if (PropertyWalk.GetString(cdo, MappingKeyField) is not { } key) return null;

        var matches = Mappings.FindAll(key);
        if (matches.Count == 0) return null;

        var distinct = matches.Select(m => m.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (distinct.Length == 1)
        {
            var shared = LoadAugmentation(distinct[0], icons);
            foreach (var variant in variants) variant.GrantedAbility = shared;
            return shared;
        }

        // Different abilities per hero. An item that names no heroes of its own is for all of them
        // and gets a variant per mapping row, because the tables are the only place that
        // difference is recorded.
        foreach (var (hero, assetPath) in matches)
        {
            var variant = variants.FirstOrDefault(v => v.Hero.Equals(hero, StringComparison.OrdinalIgnoreCase));
            if (variant is null)
            {
                if (restricted) continue;
                variant = new HeroVariant(hero, Heroes.Shipped(hero));
                variants.Add(variant);
            }

            variant.GrantedAbility = LoadAugmentation(assetPath, icons);
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
            if (!seen.Add($"{property.Name}|{path}")) continue;

            found.Add(GameplayEffects.Resolve(_provider, _constants, property.Name, path));
        }

        return found;
    }

    /// <summary>The abilities an asset grants directly, each resolved in full.</summary>
    private List<AbilityDetails> GrantedAbilities(UObject asset, IconWriter? icons) =>
        [.. PropertyWalk.Walk(asset)
            .Where(p => p.Path.StartsWith("GrantedAbilities[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => ResolveAbility(path, icons))];

    /// <summary>A legendary's bonus stats, each its own augmentation asset.</summary>
    private List<Augmentation> LegendaryStats(UObject cdo, IconWriter? icons) =>
        [.. PropertyWalk.Walk(cdo)
            .Where(p => p.Path.StartsWith("LegendaryStats[", StringComparison.Ordinal))
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Select(path => LoadAugmentation(path, icons))];

    /// <summary>
    /// A character augmentation and, when it carries no text of its own, the abilities it grants.
    /// An item's granted ability is usually a bare wrapper around a gameplay ability, so stopping
    /// at the augmentation returns an empty result for the tooltips with the most to show.
    /// </summary>
    public Augmentation LoadAugmentation(string assetPath, IconWriter? icons)
    {
        var package = AbilityPresentationIndex.PackagePath(assetPath);
        if (!_provider.TryLoadPackage(package, out var loaded))
        {
            return new Augmentation(assetPath) { Unresolved = $"could not load {package}" };
        }

        var export = loaded.GetExports().FirstOrDefault();
        if (export is null)
        {
            return new Augmentation(assetPath) { Unresolved = $"no export in {package}" };
        }

        var abilities = GrantedAbilities(export, icons);

        return new Augmentation(assetPath)
        {
            DisplayName = PropertyWalk.GetString(export, "DisplayName"),
            Description = PropertyWalk.GetString(export, "Description"),
            Abilities = Empty(abilities),
            GrantedGameplayTags = PropertyWalk.GetTags(export, "GrantedGameplayTags"),
            Properties = UeJson.ToElement(export),
        };
    }

    /// <summary>One gameplay ability, resolved the same way the top-level command resolves one.</summary>
    public AbilityDetails ResolveAbility(string assetPath, IconWriter? icons)
    {
        var package = AbilityPresentationIndex.PackagePath(assetPath);
        if (!_provider.TryLoadPackage(package, out var loaded))
        {
            return new AbilityDetails(assetPath) { Unresolved = $"could not load {package}" };
        }

        var exports = loaded.GetExports().ToArray();
        var cdo = exports.FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();

        var name = AbilityPresentationIndex.AssetName(assetPath);
        var presentation = Presentation.Find(name);
        var display = Display.From(presentation?.Row, cdo);
        var bindings = cdo is null ? [] : CurveBindings.Collect(cdo, _constants);

        return new AbilityDetails(assetPath)
        {
            DisplayName = display.Name,
            Description = display.Description,
            Detail = display.Detail.Count == 0 ? null : display.Detail,
            Icon = display.Icon,
            IconImage = display.Icon is null ? null : icons?.Invoke(display.Icon, name),
            PresentationTable = presentation?.Table,
            PresentationRow = presentation?.RowName,
            MatchedBy = presentation?.MatchedBy,
            Targeting = cdo is null ? null : Targeting.From(cdo),
            Effects = cdo is null ? null : Empty(Effects(cdo)),
            DescriptionTokens = presentation is null
                ? null
                : Empty(DescriptionTokens.Resolve(presentation.Row, _provider, _constants)),
            Constants = Empty(bindings),
            Properties = cdo is null ? null : UeJson.ToElement(cdo),
            Unresolved = cdo is null ? $"no export in {package}" : null,
        };
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
