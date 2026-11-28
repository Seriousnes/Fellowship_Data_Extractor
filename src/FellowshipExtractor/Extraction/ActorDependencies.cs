using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The two effect classes of a main effect's <c>CRActorDependencyComponent</c>, by asset name. Shipping exe
/// 26320967: the component's effect-added routine 0x1452EF3E0 binds two listeners for each class, on the delegates
/// the damage driver calls on a hit's target before the attribute gather (character +0x848) and after the hit
/// (+0x860). The first listener (0x1452F04A0 for the owner's class, 0x1452F0820 for the instigator's) applies the
/// class to its character with the main effect's stack count, and the second removes it. The owner's class is on the
/// main effect's owner for a hit whose source is the main effect's instigator. The instigator's class is on the
/// instigator for a hit whose source is the main effect's owner.
/// </summary>
public sealed record ActorDependency(string? AppliedToTheInstigator, string? AppliedToTheOwner);

public static class ActorDependencies
{
    public const string NativeClass = "/Script/fellowship.CRGameplayEffectActorDependency";

    private const string Component = "CRActorDependencyComponent";
    private const string InstigatorField = "EffectAppliedToTheInstigatorOfTheMainEffect";
    private const string OwnerField = "EffectAppliedToTheOwnerOfTheMainEffect";
    private const string ClassSuffix = "_C";

    /// <summary>Each class from the nearest class of the effect's chain with a component stating it.</summary>
    public static ActorDependency? Of(UObject effect, AbstractFileProvider provider)
    {
        string? instigator = null;
        string? owner = null;

        foreach (var holder in ClassDefaults.Chain(effect, provider))
        {
            foreach (var export in holder.Owner?.GetExports() ?? [])
            {
                if (export.ExportType != Component) continue;

                instigator ??= AssetName(PropertyWalk.GetString(export, InstigatorField));
                owner ??= AssetName(PropertyWalk.GetString(export, OwnerField));
            }

            if (instigator is not null && owner is not null) break;
        }

        return instigator is null && owner is null ? null : new ActorDependency(instigator, owner);
    }

    /// <summary>True for an effect whose native class is <c>CRGameplayEffectActorDependency</c>.</summary>
    public static bool IsDependent(UObject effect, AbstractFileProvider provider) =>
        ClassDefaults.NativeParent(effect, provider) == NativeClass;

    private static string? AssetName(string? classPath)
    {
        if (classPath is null) return null;

        var name = classPath[(classPath.LastIndexOfAny(['.', '/']) + 1)..];

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }
}
