using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public static class CooldownTerms
{
    public const string PreAdditives = "CooldownPreAdditives";
    public const string Multipliers = "CooldownMultipliers";
    public const string PostAdditives = "CooldownPostAdditives";

    private const string ModifierField = "Modifier";
    private const string RequiredTagField = "RequiredGrantedTag";
    private const string TagNameField = "TagName";

    private static readonly string[] Arrays = [PreAdditives, Multipliers, PostAdditives];

    public static IReadOnlyList<CooldownTerm> Of(UObject? cdo, AbstractFileProvider provider, ConstantsTable constants)
    {
        if (cdo is null) return [];

        var terms = new List<CooldownTerm>();

        foreach (var array in Arrays)
        {
            if (ClassDefaults.Inherited(cdo, array, provider)?.GenericValue is not UScriptArray entries) continue;

            foreach (var entry in entries.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder held }) continue;
                if (PropertyWalk.GetStruct(held, ModifierField) is not { } scalable) continue;
                if (MagnitudeCalculations.Scalable(scalable, constants) is not { } value) continue;

                var tag = PropertyWalk.GetStruct(held, RequiredTagField) is { } required
                    ? PropertyWalk.GetString(required, TagNameField)
                    : null;

                terms.Add(new CooldownTerm(array, value)
                {
                    Tag = tag is { Length: > 0 } && !tag.Equals("None", StringComparison.Ordinal) ? tag : null,
                });
            }
        }

        return terms;
    }
}
