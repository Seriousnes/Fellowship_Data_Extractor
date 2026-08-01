using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// A gameplay effect an ability applies, with the attributes it scales off and the tuning
/// constants behind those coefficients.
/// </summary>
public sealed record EffectRef(string Property, string Object)
{
    /// <summary>The attributes the effect captures, e.g. Strength for a Strength-scaling hit.</summary>
    public IReadOnlyList<string>? CapturedAttributes { get; init; }

    public IReadOnlyList<CurveBinding>? Constants { get; init; }
    public string? Unresolved { get; init; }
}

/// <summary>
/// Reading a gameplay effect for its scaling. An ability's damage is never on the ability: the
/// effect it applies holds the coefficient and names the attribute the coefficient multiplies,
/// and both reach their numbers through the same curve rows as everything else.
/// </summary>
public static class GameplayEffects
{
    private const string AttributeField = "AttributeName";

    public static EffectRef Resolve(
        AbstractFileProvider provider, ConstantsTable constants, string property, string objectPath)
    {
        var result = new EffectRef(property, objectPath);

        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return result with { Unresolved = $"could not load {objectPath}" };
        }

        var effect = package.GetExports()
            .FirstOrDefault(e => e.Name.StartsWith("Default__", StringComparison.Ordinal));
        if (effect is null) return result with { Unresolved = $"no class default object in {objectPath}" };

        return result with
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
