using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// A {Token} in a description, resolved as far as the shipped data allows: the gameplay effect
/// it draws from, the attribute that effect captures, and every tuning constant behind it.
/// </summary>
public sealed record ResolvedToken(string TokenName)
{
    public string? Object { get; init; }

    /// <summary>Enum ordinals. The usmap carries no enum table yet, so these have no names.</summary>
    public string? CalculationOrdinal { get; init; }
    public string? TypeOrdinal { get; init; }

    public string? Attribute { get; init; }
    public IReadOnlyList<string>? CapturedAttributes { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }
    public string? Unresolved { get; init; }
}

public static class DescriptionTokens
{
    public static List<ResolvedToken> Resolve(
        IPropertyHolder row, AbstractFileProvider provider, ConstantsTable constants)
    {
        var resolved = new List<ResolvedToken>();

        if (PropertyWalk.GetStruct(row, "DynamicDescription") is not { } dynamic) return resolved;
        if (PropertyWalk.Get(dynamic, "Tokens")?.GenericValue is not UScriptArray tokens) return resolved;

        foreach (var entry in tokens.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder token }) continue;

            var name = PropertyWalk.GetString(token, "TokenName");
            if (name is null) continue;

            resolved.Add(ResolveOne(name, token, provider, constants));
        }

        return resolved;
    }

    private static ResolvedToken ResolveOne(
        string name, IPropertyHolder token, AbstractFileProvider provider, ConstantsTable constants)
    {
        var objectPath = PropertyWalk.GetString(token, "Object");

        var result = new ResolvedToken(name)
        {
            Object = objectPath,
            CalculationOrdinal = PropertyWalk.GetString(token, "Calculation"),
            TypeOrdinal = PropertyWalk.GetString(token, "Type"),
            Attribute = PropertyWalk.GetStruct(token, "Attribute") is { } attribute
                ? PropertyWalk.GetString(attribute, "AttributeName")
                : null,
        };

        if (objectPath is null) return result;

        // The token points at the gameplay effect that produces the number, and it is read exactly
        // as an effect the ability references directly is, so both routes report the same thing.
        var effect = GameplayEffects.Resolve(provider, constants, name, objectPath);

        return result with
        {
            Constants = effect.Constants,
            CapturedAttributes = effect.CapturedAttributes,
            Unresolved = effect.Unresolved,
        };
    }
}
