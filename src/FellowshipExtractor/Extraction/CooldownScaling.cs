using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record CooldownTiming(IReadOnlyList<string>? DurationScalesOff, IReadOnlyList<string>? TimeRateScalesOff);

public sealed class CooldownScaling
{
    public const string HasteAttribute = "Haste";

    private const string CooldownClassField = "CooldownEffectBaseClass";
    private const string ChargeClassField = "ChargeGameplayEffectClass";
    private const string HeroAttributesEffect = "GE_HeroAttributesBase";
    private const string ModsField = "AttributeMods";
    private const string AttributeTypeField = "AttributeType";
    private const string TimeRate = "ECREffectAttributeType::TimeRate";
    private const string EffectTagField = "EffectTag";
    private const string MagnitudeField = "AttributeMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string ScalableFloatKind = "EGameplayEffectMagnitudeCalculation::ScalableFloat";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string AttributeBasedField = "AttributeBasedMagnitude";
    private const string BackingField = "BackingAttribute";
    private const string CaptureField = "AttributeToCapture";
    private const string AttributeNameField = "AttributeName";
    private const string DurationField = "DurationMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string CalculationField = "CalculationClassMagnitude";
    private const string CalculationFunction = "CalculateBaseMagnitude";
    private const string CombinedField = "CombinedTags";
    private const string AddedField = "Added";
    private const string RemovedField = "Removed";
    private const string ParentClassTag = "ParentClass";
    private const string CooldownTypeRoot = "CooldownType.";
    private const string ScriptRoot = "/Script/";
    private const char Quote = '\'';

    private static readonly string[] TagContainers = ["InheritableAssetTags", "InheritableGameplayEffectTags"];

    private readonly AbstractFileProvider _provider;
    private readonly ConstantsTable _constants;
    private readonly IReadOnlyDictionary<string, string> _timeRateAttributes;
    private readonly ILookup<string, string> _childrenByParent;
    private readonly Dictionary<string, CooldownTiming?> _timings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _cooldownTypes = new(StringComparer.OrdinalIgnoreCase);

    public CooldownScaling(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, ConstantsTable constants)
    {
        _provider = provider;
        _constants = constants;

        var heroAttributes = assets.FirstOrDefault(
            asset => asset.AssetName.Text.Equals(HeroAttributesEffect, StringComparison.OrdinalIgnoreCase));

        _timeRateAttributes = heroAttributes is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : TimeRateAttributes(provider, heroAttributes.PackageName.Text);

        if (_timeRateAttributes.Count == 0)
        {
            throw new InvalidOperationException(
                $"{HeroAttributesEffect} declares no {TimeRate} modifier, so nothing states which attribute speeds " +
                "up an effect carrying a time rate tag, and every cooldown would read as unscaled.");
        }

        _childrenByParent = assets
            .Select(asset => (Parent: Quoted(AssetRegistry.TagValue(asset, ParentClassTag)), Package: asset.PackageName.Text))
            .Where(pair => pair.Parent is not null)
            .Distinct()
            .ToLookup(pair => pair.Parent!, pair => pair.Package, StringComparer.OrdinalIgnoreCase);
    }

    public CooldownTiming? Of(UObject ability, double? charges)
    {
        var governing = charges > 1 ? ClassOf(ability, ChargeClassField) : ClassOf(ability, CooldownClassField);

        return governing is null ? null : Timing(governing);
    }

    public CooldownTiming? Charged(UObject ability) =>
        ClassOf(ability, ChargeClassField) is { } charge ? Timing(charge) : null;

    private string? ClassOf(UObject ability, string field) =>
        PropertyWalk.AsString(ClassDefaults.Inherited(ability, field, _provider)?.GenericValue);

    private CooldownTiming? Timing(string classPath)
    {
        if (_timings.TryGetValue(classPath, out var known)) return known;

        var timing = Tags(classPath) is { } tags
            ? new CooldownTiming(
                Listed(DurationReads(classPath)),
                Listed(tags.Select(tag => _timeRateAttributes.GetValueOrDefault(tag)).OfType<string>()))
            : null;

        _timings[classPath] = timing;
        return timing;
    }

    private HashSet<string>? Tags(string classPath)
    {
        if (!classPath.StartsWith(ScriptRoot, StringComparison.Ordinal))
        {
            return ClassDefaults.Load(classPath, _provider) is { } effect ? Containers(effect, CombinedField) : null;
        }

        HashSet<string>? agreed = null;

        foreach (var child in _childrenByParent[classPath])
        {
            if (ClassDefaults.Load(child, _provider) is not { } effect) continue;

            var inherited = Containers(effect, CombinedField);
            inherited.UnionWith(Containers(effect, RemovedField));
            inherited.ExceptWith(Containers(effect, AddedField));

            if (agreed is null) agreed = inherited;
            else if (!agreed.SetEquals(inherited)) return null;
        }

        return agreed;
    }

    public static IReadOnlySet<string> AssetTags(UObject effect) => Containers(effect, CombinedField);

    private static HashSet<string> Containers(UObject effect, string field)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var exports = effect.Owner?.GetExports().ToArray() ?? [effect];
        var componentsCarry = exports.Any(export => export != effect && Carries(export));

        foreach (var export in exports)
        {
            if (componentsCarry && export == effect) continue;

            foreach (var container in TagContainers)
            {
                if (PropertyWalk.GetStruct(export, container) is not { } inherited) continue;

                tags.UnionWith(PropertyWalk.GetTags(inherited, field) ?? []);
            }
        }

        return tags;
    }

    private static bool Carries(UObject export) =>
        TagContainers.Any(container => PropertyWalk.GetStruct(export, container) is not null);

    private IEnumerable<string> DurationReads(string classPath)
    {
        if (ClassDefaults.Load(classPath, _provider) is not { } effect) yield break;
        if (Struct(ClassDefaults.Inherited(effect, DurationField, _provider)) is not { } duration) yield break;
        if (PropertyWalk.GetStruct(duration, CustomField) is not { } custom) yield break;
        if (PropertyWalk.GetString(custom, CalculationField) is not { } calculationPath) yield break;
        if (ClassDefaults.Load(calculationPath, _provider) is not { } calculation) yield break;
        if (BareStateWalk.Compiled(calculation, CalculationFunction) is not { ScriptBytecode.Length: > 0 } body)
        {
            yield break;
        }

        var statements = new BareStateWalk(body, calculation, _constants, null).Statements();

        foreach (var attribute in MagnitudeCalculations.ReadCaptures(calculation, _provider, statements))
        {
            yield return attribute;
        }
    }

    public IReadOnlyList<string> CooldownTypesAccelerated(string effectClass)
    {
        if (_cooldownTypes.TryGetValue(effectClass, out var known)) return known;

        IReadOnlyList<string> tags = ClassDefaults.Load(effectClass, _provider)?.Owner is { } package
            ?
            [
                .. TimeRateMods(package.GetExports())
                    .Select(mod => mod.Tag)
                    .Where(tag => tag.StartsWith(CooldownTypeRoot, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal),
            ]
            : [];

        return _cooldownTypes[effectClass] = tags;
    }

    /// <summary>
    /// The scalable-float TimeRate mods on <paramref name="effect"/>'s components. A mod reading an attribute, a
    /// calculation class or a set-by-caller magnitude has no value of its own here.
    /// </summary>
    public static IReadOnlyList<TimeRateModifier> TimeRates(UObject effect, ConstantsTable constants)
    {
        var rates = new List<TimeRateModifier>();

        foreach (var (tag, mod) in TimeRateMods(effect.Owner?.GetExports() ?? [effect]))
        {
            if (PropertyWalk.GetStruct(mod, MagnitudeField) is not { } magnitude) continue;

            var kind = PropertyWalk.GetString(magnitude, CalculationTypeField);
            if (kind is not (null or ScalableFloatKind)) continue;

            if (PropertyWalk.GetStruct(magnitude, ScalableField) is not { } scalable) continue;
            if (MagnitudeCalculations.Scalable(scalable, constants) is not { } value) continue;

            var rate = new TimeRateModifier(tag, Math.Round(value, 10));
            if (!rates.Contains(rate)) rates.Add(rate);
        }

        return rates;
    }

    private static Dictionary<string, string> TimeRateAttributes(AbstractFileProvider provider, string package)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!provider.TryLoadPackage(package, out var loaded)) return attributes;

        foreach (var (tag, mod) in TimeRateMods(loaded.GetExports()))
        {
            if (Backing(mod) is { } attribute) attributes.TryAdd(tag, attribute);
        }

        return attributes;
    }

    private static IEnumerable<(string Tag, IPropertyHolder Mod)> TimeRateMods(IEnumerable<UObject> exports)
    {
        foreach (var export in exports)
        {
            if (PropertyWalk.Get(export, ModsField)?.GenericValue is not UScriptArray mods) continue;

            foreach (var element in mods.Properties)
            {
                if (Struct(element) is not { } mod) continue;
                if (PropertyWalk.GetString(mod, AttributeTypeField) != TimeRate) continue;
                if (PropertyWalk.GetString(mod, EffectTagField) is not { } tag) continue;

                yield return (tag, mod);
            }
        }
    }

    private static string? Backing(IPropertyHolder mod) =>
        PropertyWalk.GetStruct(mod, MagnitudeField) is { } magnitude &&
        PropertyWalk.GetStruct(magnitude, AttributeBasedField) is { } based &&
        PropertyWalk.GetStruct(based, BackingField) is { } backing &&
        PropertyWalk.GetStruct(backing, CaptureField) is { } capture
            ? PropertyWalk.GetString(capture, AttributeNameField)
            : null;

    private static IPropertyHolder? Struct(FPropertyTagType? tag) => tag?.GenericValue switch
    {
        FScriptStruct { StructType: IPropertyHolder inner } => inner,
        IPropertyHolder direct => direct,
        _ => null,
    };

    private static string? Quoted(string? reference)
    {
        if (reference is null) return null;

        var open = reference.IndexOf(Quote);
        var close = reference.LastIndexOf(Quote);

        return open >= 0 && close > open ? reference[(open + 1)..close] : null;
    }

    private static IReadOnlyList<string>? Listed(IEnumerable<string> attributes) =>
        attributes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } listed
            ? listed
            : null;
}
