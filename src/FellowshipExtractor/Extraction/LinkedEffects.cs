using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

/// <summary>One entry of a <c>CRLinkedEffectComponent</c>: the parent applies the child when the tags allow it.</summary>
public sealed record LinkedEffect(
    string Parent,
    string Child,
    string ApplyTo,
    IReadOnlyList<string> RequiredSourceTags,
    IReadOnlyList<string> RequiredTargetTags,
    IReadOnlyList<string> BannedSourceTags);

/// <summary>Reads <c>CRLinkedEffectComponent.AdditionalEffectsToApply</c> on a gameplay effect's package.</summary>
public static class LinkedEffects
{
    private const string Component = "CRLinkedEffectComponent";
    private const string EntriesField = "AdditionalEffectsToApply";
    private const string EffectClassField = "EffectClass";
    private const string ApplyToField = "ApplyTo";
    private const string RequiredSourceTagsField = "RequiredSourceTags";
    private const string RequiredTargetTagsField = "RequiredTargetTags";
    private const string BannedSourceTagsField = "BannedSourceTags";
    private const string CaptureSourceEnum = "EGameplayEffectAttributeCaptureSource";
    private const string DefaultReceiver = "Source";

    public static IEnumerable<LinkedEffect> Of(IPackage package, string parent, TypeMappings mappings)
    {
        foreach (var export in package.GetExports())
        {
            if (export.ExportType != Component) continue;
            if (PropertyWalk.Get(export, EntriesField)?.GenericValue is not UScriptArray entries) continue;

            foreach (var entry in entries.Properties)
            {
                if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder link }) continue;
                if (PropertyWalk.GetString(link, EffectClassField) is not { } path) continue;

                var receiver = EnumOrdinals.Member(mappings, CaptureSourceEnum, PropertyWalk.GetString(link, ApplyToField));

                yield return new LinkedEffect(
                    parent,
                    Leaf(path),
                    receiver ?? DefaultReceiver,
                    PropertyWalk.GetTags(link, RequiredSourceTagsField) ?? [],
                    PropertyWalk.GetTags(link, RequiredTargetTagsField) ?? [],
                    PropertyWalk.GetTags(link, BannedSourceTagsField) ?? []);
            }
        }
    }

    private static string Leaf(string path)
    {
        var name = AbilityPresentationIndex.AssetName(path);

        return name.EndsWith("_C", StringComparison.Ordinal) ? name[..^2] : name;
    }
}
