using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;

namespace FellowshipExtractor.Extraction;

public sealed record GlobalCooldown(double Seconds, IReadOnlyList<string>? ScalesOff);

/// <summary>
/// The global cooldown an ability starts: the duration of its <c>GlobalCooldownGameplayEffectClass</c>. The native
/// default of that field is no class, so an ability declaring none, here or on a Blueprint parent, starts no global
/// cooldown. Each field of the duration is the one the nearest effect in the class chain declares, because a child
/// effect states only the fields it changes: <c>GE_GlobalCooldown_TariqHeavySingleTarget</c> states its coefficient
/// and takes its calculation class from <c>GE_GlobalCooldown</c>. A calculation class is evaluated with each captured
/// attribute at the value every hero starts it at (Haste 1, CooldownReduction 0), and an attribute whose rise by 1
/// changes the duration is one the global cooldown scales off.
/// </summary>
public sealed class GlobalCooldowns(AbstractFileProvider provider, ConstantsTable constants, Func<string, double?> starting)
{
    private const string ClassField = "GlobalCooldownGameplayEffectClass";
    private const string DurationField = "DurationMagnitude";
    private const string CalculationTypeField = "MagnitudeCalculationType";
    private const string ScalableField = "ScalableFloatMagnitude";
    private const string CustomField = "CustomMagnitude";
    private const string CalculationField = "CalculationClassMagnitude";
    private const string CoefficientField = "Coefficient";
    private const string PreMultiplyField = "PreMultiplyAdditiveValue";
    private const string PostMultiplyField = "PostMultiplyAdditiveValue";
    private const string CalculationFunction = "CalculateBaseMagnitude";
    private const string CustomType = "EGameplayEffectMagnitudeCalculation::CustomCalculationClass";
    private const string NoClass = "None";
    private const double Rise = 1;

    private readonly Dictionary<string, GlobalCooldown?> _byClass = new(StringComparer.OrdinalIgnoreCase);

    public int Read { get; private set; }

    public int Unread { get; private set; }

    public GlobalCooldown? Of(UObject? ability)
    {
        if (ability is null) return null;
        if (PropertyWalk.AsString(ClassDefaults.Inherited(ability, ClassField, provider)?.GenericValue) is not { } classPath ||
            classPath.Length == 0 ||
            classPath.Equals(NoClass, StringComparison.Ordinal))
        {
            return null;
        }

        if (!_byClass.TryGetValue(classPath, out var read))
        {
            read = _byClass[classPath] = Duration(classPath);
            if (read is null) Unread++;
        }

        if (read is not null) Read++;
        return read;
    }

    private GlobalCooldown? Duration(string classPath)
    {
        if (ClassDefaults.Load(classPath, provider) is not { } effect) return null;

        var durations = ClassDefaults.Chain(effect, provider)
            .Select(holder => Struct(PropertyWalk.Get(holder, DurationField)))
            .OfType<IPropertyHolder>()
            .ToArray();

        var customs = durations
            .Select(duration => PropertyWalk.GetStruct(duration, CustomField))
            .OfType<IPropertyHolder>()
            .ToArray();

        var type = durations.Select(duration => PropertyWalk.GetString(duration, CalculationTypeField)).FirstOrDefault(found => found is not null);

        if (type != CustomType)
        {
            return durations.Select(duration => PropertyWalk.GetStruct(duration, ScalableField)).FirstOrDefault(found => found is not null) is { } scalable &&
                   MagnitudeCalculations.Scalable(scalable, constants) is { } seconds
                ? new GlobalCooldown(seconds, null)
                : null;
        }

        if (customs.Select(custom => PropertyWalk.GetString(custom, CalculationField)).FirstOrDefault(found => found is not null) is not { } calculationPath ||
            ClassDefaults.Load(calculationPath, provider) is not { } calculation ||
            BareStateWalk.Compiled(calculation, CalculationFunction) is not { ScriptBytecode.Length: > 0 } body)
        {
            return null;
        }

        if (Scaled(customs, CoefficientField, 1) is not { } coefficient ||
            Scaled(customs, PreMultiplyField, 0) is not { } pre ||
            Scaled(customs, PostMultiplyField, 0) is not { } post)
        {
            return null;
        }

        double? Evaluated(Func<string, double?> captured) =>
            new BareStateWalk(body, calculation, constants, null) { Effect = EffectContext.For(effect, captured) }.Answer() is { } answer
                ? Math.Round(coefficient * (answer + pre) + post, 10)
                : null;

        var captures = MagnitudeCalculations
            .ReadCaptures(calculation, provider, new BareStateWalk(body, calculation, constants, null).Statements())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (captures.Any(attribute => starting(attribute) is null)) return null;
        if (Evaluated(starting) is not { } unscaled) return null;

        var scalesOff = captures
            .Where(attribute => Evaluated(name => starting(name) + (name == attribute ? Rise : 0)) is { } changed && changed != unscaled)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new GlobalCooldown(unscaled, scalesOff.Length == 0 ? null : scalesOff);
    }

    private double? Scaled(IEnumerable<IPropertyHolder> customs, string field, double fallback) =>
        customs.Select(custom => PropertyWalk.GetStruct(custom, field)).FirstOrDefault(found => found is not null) is { } scalable
            ? MagnitudeCalculations.Scalable(scalable, constants)
            : fallback;

    private static IPropertyHolder? Struct(FPropertyTagType? tag) => tag?.GenericValue switch
    {
        FScriptStruct { StructType: IPropertyHolder inner } => inner,
        IPropertyHolder direct => direct,
        _ => null,
    };
}
