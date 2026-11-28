using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The health policies a gameplay effect's <c>CRAttributeModComponent</c> states for a change of <c>MaxHealth</c>: the
/// <c>EMaxHealthModificationHealthPatchPolicy</c> member of <c>MaxHealthModificationPolicy</c>, and the
/// <c>EMaxHealthModificationHealthReductionPolicy</c> member of <c>MaxHealthOnRemovedPolicy</c>. A field the component
/// leaves at the class default is null, because the cooked asset omits it and the default is native code's.
/// </summary>
public sealed record MaxHealthPolicy(string? OnModification, string? OnRemoved);

public static class MaxHealthPolicies
{
    private const string Component = "CRAttributeModComponent";
    private const string ModificationField = "MaxHealthModificationPolicy";
    private const string ModificationEnum = "EMaxHealthModificationHealthPatchPolicy";
    private const string RemovedField = "MaxHealthOnRemovedPolicy";
    private const string RemovedEnum = "EMaxHealthModificationHealthReductionPolicy";

    /// <summary>
    /// Each policy from the nearest class of the effect's chain with a component stating it. The archetype of a
    /// Blueprint child's component is its parent's component, so the child's cooked export omits a field equal to
    /// the parent's.
    /// </summary>
    public static MaxHealthPolicy? Of(UObject effect, AbstractFileProvider provider, TypeMappings mappings)
    {
        string? modification = null;
        string? removed = null;

        foreach (var holder in ClassDefaults.Chain(effect, provider))
        {
            foreach (var export in holder.Owner?.GetExports() ?? [])
            {
                if (export.ExportType != Component) continue;

                modification ??= Stated(export, ModificationField, ModificationEnum, mappings);
                removed ??= Stated(export, RemovedField, RemovedEnum, mappings);
            }

            if (modification is not null && removed is not null) break;
        }

        return modification is null && removed is null ? null : new MaxHealthPolicy(modification, removed);
    }

    private static string? Stated(UObject component, string field, string enumName, TypeMappings mappings)
    {
        if (PropertyWalk.GetString(component, field) is not { Length: > 0 } text) return null;

        return EnumOrdinals.Member(mappings, enumName, text)
            ?? throw new InvalidOperationException(
                $"{component.GetPathName()} states {field} = {text}, and {enumName} has no such member in the mappings.");
    }
}
