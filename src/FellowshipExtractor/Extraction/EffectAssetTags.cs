using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// A gameplay effect's asset tags: the <c>CombinedTags</c> of its <c>AssetTagsGameplayEffectComponent</c>.
/// Shipping exe 26320967: the component's load routines 0x144AAFE60 and 0x144AB4390 rebuild <c>CombinedTags</c> from
/// the parent class's component (0x144A91D70) and copy it to the effect's cached asset tags at +0xA28 (0x144A6B880).
/// <c>FGameplayEffectSpec::GetAllAssetTags</c> 0x144A76EA0 reads that container.
/// </summary>
public static class EffectAssetTags
{
    private const string Component = "AssetTagsGameplayEffectComponent";
    private const string ComponentsField = "GEComponents";
    private const string ComponentContainer = "InheritableAssetTags";
    private const string EffectContainer = "InheritableGameplayEffectTags";
    private const string CombinedTagsField = "CombinedTags";

    /// <summary>
    /// The tags in the asset's order. The cooked export of a class omits a member equal to its parent's, so the tags
    /// are those of the nearest class of the effect's chain with a component stating <c>CombinedTags</c>. A class
    /// with a <c>GEComponents</c> list and no such component in it has no asset tags. Where every component of the
    /// chain is at its native class default, the tags are those of the nearest <c>InheritableGameplayEffectTags</c>,
    /// the field the editor writes the same tags to.
    /// </summary>
    public static IReadOnlyList<string> Of(UObject effect, AbstractFileProvider provider)
    {
        var chain = ClassDefaults.Chain(effect, provider).ToArray();

        foreach (var holder in chain)
        {
            if (PropertyWalk.Get(holder, ComponentsField)?.GenericValue is not UScriptArray components) continue;

            var listed = components.Properties
                .Select(element => (element?.GenericValue as FPackageIndex)?.ResolvedObject?.Name.Text)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            var component = (holder.Owner?.GetExports() ?? [])
                .FirstOrDefault(export => export.ExportType == Component && listed.Contains(export.Name));

            if (component is null) return [];
            if (Combined(component, ComponentContainer) is { } combined) return combined;
        }

        foreach (var holder in chain)
        {
            if (Combined(holder, EffectContainer) is { } written) return written;
        }

        return [];
    }

    private static string[]? Combined(IPropertyHolder holder, string container) =>
        PropertyWalk.GetStruct(holder, container) is { } inherited
            ? PropertyWalk.GetTags(inherited, CombinedTagsField)
            : null;
}
