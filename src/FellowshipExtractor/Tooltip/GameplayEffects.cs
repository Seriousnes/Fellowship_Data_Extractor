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

    /// <summary>The band this effect rolls its magnitude in, from the preset row it names.</summary>
    public double? Spread { get; init; }
}

/// <summary>
/// Reading a gameplay effect for its scaling. An ability's damage is never on the ability: the
/// effect it applies holds the coefficient and names the attribute the coefficient multiplies,
/// and both reach their numbers through the same curve rows as everything else.
/// </summary>
public static class GameplayEffects
{
    private const string AttributeField = "AttributeName";
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";

    /// <summary>
    /// The gameplay effects an asset references, each as the name the tag table knows it by and the
    /// path that reaches it. An ability holds its damage effect as an ordinary class reference -
    /// DamageEffect, DamageEffectRef, DotEffect and a dozen other names designers chose - so the
    /// reference is recognised by its target being a known effect rather than by the field it sits
    /// in.
    ///
    /// Both directions of the relation are read from here. The resolver follows it forwards to fold
    /// an effect's numbers into the entity that applies them, and <see cref="EffectIconSources"/>
    /// inverts it to find the entity an effect can take an icon from, so the two can never disagree
    /// about what counts as a reference.
    /// </summary>
    public static IEnumerable<(string Name, string Path)> ReferencedIn(
        UObject asset, IReadOnlySet<string> effectNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in PropertyWalk.Walk(asset))
        {
            if (PropertyWalk.AsString(property.Value) is not { } path) continue;

            // A native class shares its name with the effect tag but has no package to read, so a
            // "/Script/..." reference is a class the effect derives from, not an effect.
            if (!path.Contains('/') || path.StartsWith(ScriptRoot, StringComparison.OrdinalIgnoreCase)) continue;

            var name = AbilityPresentationIndex.AssetName(path);
            if (name.EndsWith(ClassSuffix, StringComparison.Ordinal)) name = name[..^ClassSuffix.Length];

            if (!effectNames.Contains(name)) continue;
            if (!seen.Add(path)) continue;

            yield return (name, path);
        }
    }

    public static EffectRef Resolve(
        AbstractFileProvider provider,
        ConstantsTable constants,
        EffectCalculationPresets presets,
        string objectPath)
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
            Spread = presets.Of(effect),
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
