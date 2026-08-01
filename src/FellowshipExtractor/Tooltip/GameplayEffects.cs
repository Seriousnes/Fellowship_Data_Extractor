using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// A gameplay effect an asset applies, reduced to what it contributes: the attributes it scales
/// off and the coefficients behind them. The effect is its own entity elsewhere in the export, so
/// its identity is not repeated here.
/// </summary>
public sealed record EffectRef
{
    public IReadOnlyList<string>? CapturedAttributes { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }
}

/// <summary>
/// Reading a gameplay effect for its scaling. An ability's damage is never on the ability: the
/// effect it applies holds the coefficient and names the attribute the coefficient multiplies,
/// and both reach their numbers through the same curve rows as everything else.
/// </summary>
public static class GameplayEffects
{
    private const string AttributeField = "AttributeName";

    public static EffectRef Resolve(AbstractFileProvider provider, ConstantsTable constants, string objectPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return new EffectRef();
        }

        var effect = package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
        if (effect is null) return new EffectRef();

        return new EffectRef
        {
            CapturedAttributes = Captures(effect),
            Constants = CurveBindings.Collect(effect, constants) is { Count: > 0 } bindings ? bindings : null,
        };
    }

    /// <summary>The attributes an effect captures, wherever in its executions and modifiers they sit.</summary>
    public static IReadOnlyList<string>? Captures(UObject effect)
    {
        var attributes = PropertyWalk.Walk(effect)
            .Where(p => p.Name == AttributeField)
            .Select(p => PropertyWalk.AsString(p.Value))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return attributes.Length == 0 ? null : attributes;
    }
}
