using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

public sealed record GlobalCooldown(double Duration, bool ScalesWithHaste);

public sealed class GlobalCooldowns
{
    private const string ClassField = "GlobalCooldownGameplayEffectClass";
    private const string DurationField = "DurationMagnitude";
    private const string TypeField = "MagnitudeCalculationType";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string CalculationField = "CalculationClassMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string PreAddField = "PreMultiplyAdditiveValue";
    private const string PostAddField = "PostMultiplyAdditiveValue";
    private const string CalculationFunction = "CalculateBaseMagnitude";
    private const string CustomType = "EGameplayEffectMagnitudeCalculation::CustomCalculationClass";
    private const string ScalableType = "EGameplayEffectMagnitudeCalculation::ScalableFloat";
    private const string NoClass = "None";
    private const double HasteProbe = 2;

    private readonly AbstractFileProvider _provider;
    private readonly ConstantsTable _constants;
    private readonly IReadOnlyDictionary<string, double> _starting;
    private readonly Dictionary<string, GlobalCooldown?> _byClass = new(StringComparer.OrdinalIgnoreCase);

    public GlobalCooldowns(AbstractFileProvider provider, ConstantsTable constants)
    {
        _provider = provider;
        _constants = constants;
        _starting = StartingAttributes(provider);
    }

    public GlobalCooldown? Of(UObject ability, string assetName)
    {
        if (PropertyWalk.AsString(ClassDefaults.Inherited(ability, ClassField, _provider)?.GenericValue) is not { } classPath ||
            classPath.Equals(NoClass, StringComparison.Ordinal))
        {
            return null;
        }

        if (_byClass.TryGetValue(classPath, out var known)) return known;

        return _byClass[classPath] = Read(classPath, assetName);
    }

    private GlobalCooldown Read(string classPath, string assetName)
    {
        var effect = ClassDefaults.Load(classPath, _provider)
            ?? throw new InvalidOperationException(
                $"{assetName} names {classPath} as its global cooldown, and no package in the build has it.");

        var chain = ClassDefaults.Chain(effect, _provider).ToArray();
        var type = PropertyWalk.AsString(Leaf(chain, [TypeField])?.GenericValue);

        if (type == ScalableType)
        {
            var scalable = Struct(Leaf(chain, [ScalableField]))
                ?? throw Unreadable(assetName, classPath, "states no scalable float magnitude");

            var value = MagnitudeCalculations.Scalable(scalable, _constants)
                ?? throw Unreadable(assetName, classPath, "names a scalable float row the constants do not have");

            return new GlobalCooldown(value, false);
        }

        if (type != CustomType) throw Unreadable(assetName, classPath, $"has a {type ?? "missing"} duration magnitude");

        var calculationPath = PropertyWalk.AsString(Leaf(chain, [CustomField, CalculationField])?.GenericValue)
            ?? throw Unreadable(assetName, classPath, "names no calculation class");

        var calculation = ClassDefaults.Load(calculationPath, _provider)
            ?? throw Unreadable(assetName, classPath, $"names {calculationPath}, which no package in the build has");

        var body = BareStateWalk.Compiled(calculation, CalculationFunction)
            ?? throw Unreadable(assetName, classPath, $"uses {calculationPath}, which compiles no {CalculationFunction}");

        var context = new EffectContext(classPath, CooldownScaling.AssetTags(effect), Starting);
        var hasted = context with { Captured = attribute => attribute == CooldownScaling.HasteAttribute ? HasteProbe * Starting(attribute) : Starting(attribute) };

        var bare = Folded(chain, Computed(body, calculation, context) ?? throw Unreadable(assetName, classPath, $"reads {calculationPath} to no number"));
        var raised = Folded(chain, Computed(body, calculation, hasted) ?? throw Unreadable(assetName, classPath, $"reads {calculationPath} to no number at raised haste"));

        return new GlobalCooldown(bare, raised < bare);
    }

    private double? Computed(UStruct body, UObject calculation, EffectContext context) =>
        new BareStateWalk(body, calculation, _constants, null) { Effect = context }.Answer();

    private double Folded(IReadOnlyList<UObject> chain, double computed) =>
        Math.Round(
            Scaled(chain, CoefficientField, 1) * (computed + Scaled(chain, PreAddField, 0)) + Scaled(chain, PostAddField, 0),
            10);

    private double Scaled(IReadOnlyList<UObject> chain, string field, double fallback) =>
        Struct(Leaf(chain, [CustomField, field])) is { } scalable
            ? MagnitudeCalculations.Scalable(scalable, _constants) ?? fallback
            : fallback;

    private double? Starting(string attribute) => _starting.TryGetValue(attribute, out var value) ? value : null;

    private static FPropertyTagType? Leaf(IEnumerable<UObject> chain, string[] path)
    {
        foreach (var holder in chain)
        {
            IPropertyHolder? at = PropertyWalk.GetStruct(holder, DurationField);

            foreach (var segment in path[..^1])
            {
                if (at is null) break;
                at = PropertyWalk.GetStruct(at, segment);
            }

            if (at?.Properties.FirstOrDefault(property => property.Name.Text.Equals(path[^1], StringComparison.Ordinal)) is { } declared)
            {
                return declared.Tag;
            }
        }

        return null;
    }

    private static IPropertyHolder? Struct(FPropertyTagType? tag) =>
        tag?.GenericValue is FScriptStruct { StructType: IPropertyHolder held } ? held : null;

    private static Dictionary<string, double> StartingAttributes(AbstractFileProvider provider)
    {
        var table = CurveRows.Load(provider, CreatureStats.BaseTable, "the attributes a character starts with");
        var stated = new Dictionary<string, HashSet<double>>(StringComparer.Ordinal);

        foreach (var (rowName, row) in table.RowMap)
        {
            if (CurveRows.First(row) is not { } value) continue;

            var attribute = rowName.Text[(rowName.Text.LastIndexOf('.') + 1)..];
            if (!stated.TryGetValue(attribute, out var values)) stated[attribute] = values = [];
            values.Add(value);
        }

        return stated
            .Where(pair => pair.Value.Count == 1)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Single(), StringComparer.Ordinal);
    }

    private static InvalidOperationException Unreadable(string assetName, string classPath, string reason) =>
        new($"{assetName}'s global cooldown {classPath} {reason}, so its global cooldown cannot be read.");
}
