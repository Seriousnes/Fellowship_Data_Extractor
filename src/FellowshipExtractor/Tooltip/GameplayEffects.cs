using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

public sealed record EffectRef
{
    public IReadOnlyList<string>? CapturedAttributes { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }

    public double? Spread { get; init; }

    public bool TickrateIgnoresHaste { get; init; }
}

public static class GameplayEffects
{
    private const string AttributeField = "AttributeName";
    private const string ClassSuffix = "_C";
    private const string ScriptRoot = "/Script/";

    public static IEnumerable<(string Name, string Path)> ReferencedIn(
        UObject asset, IReadOnlySet<string> effectNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in PropertyWalk.Walk(asset))
        {
            if (PropertyWalk.AsString(property.Value) is not { } path) continue;

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
            TickrateIgnoresHaste = IgnoresHaste(effect),
        };
    }

    private const string HasteExemptTag = "EffectLogic.TickrateNotAffectedByHasteExclusively";

    public static bool IgnoresHaste(UObject effect) => Declares(effect, HasteExemptTag);

    private static bool Declares(UObject effect, string tag) =>
        PropertyWalk.Walk(effect)
            .Select(p => PropertyWalk.AsTags(p.Value))
            .OfType<string[]>()
            .Any(tags => tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)));

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
