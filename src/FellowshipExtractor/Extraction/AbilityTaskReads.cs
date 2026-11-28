using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The fields of an ability and of an effect the native channel task and the native ability cancel read.
/// <c>docs/casting-and-channeling.md</c> has both routines.
/// </summary>
public static class AbilityTaskReads
{
    private const string ChannelingField = "ChannelingData";
    private const string AttackSpeedField = "ScaleTimeWithAttackSpeed";
    private const string AbilityTagsField = "AbilityTags";
    private const string BlockedTagsField = "ActivationBlockedTags";

    private const string TargetTagsComponent = "TargetTagsGameplayEffectComponent";
    private const string GrantedContainerField = "InheritableGrantedTagsContainer";
    private const string OwnedContainerField = "InheritableOwnedTagsContainer";
    private const string CombinedTagsField = "CombinedTags";

    private const string ExecutionsField = "Executions";
    private const string CalculationClassField = "CalculationClass";
    private const string ClassSuffix = "_C";

    /// <summary>
    /// The constructor defaults of <c>CRChannelingTaskData</c>, for a member no class of the chain has. Shipping exe
    /// 26320967: the exec thunk of <c>ServerStartChanneling</c> stores them at 0x144F1C0E2 (<c>ContinuousChannel</c>
    /// 0), 0x144F1C138 (<c>TickImmediately</c> 0) and 0x144F1C13F (<c>ScaleDurationWithAbilityTimeRate</c> 1,
    /// <c>EnablePartialTicks</c> 0, <c>AllowSpellPushback</c> 0).
    /// </summary>
    public static readonly ChannelingTaskData Defaults = new(
        ContinuousChannel: false,
        TickImmediately: false,
        ScaleDurationWithAbilityTimeRate: true,
        EnablePartialTicks: false,
        AllowSpellPushback: false);

    /// <summary>The flags of the ability's <c>ChannelingData</c>, each from the nearest class of its chain with it.</summary>
    public static ChannelingTaskData? Channeling(UObject cdo, AbstractFileProvider provider)
    {
        var held = CreatureAbilityReads.Held([.. ClassDefaults.Chain(cdo, provider)], ChannelingField);

        if (held.Count == 0) return null;

        bool Flag(string member, bool fallback) =>
            held.Select(data => PropertyWalk.Get(data, member)?.GenericValue as bool?)
                .FirstOrDefault(value => value is not null) ?? fallback;

        return new ChannelingTaskData(
            ContinuousChannel: Flag(nameof(ChannelingTaskData.ContinuousChannel), Defaults.ContinuousChannel),
            TickImmediately: Flag(nameof(ChannelingTaskData.TickImmediately), Defaults.TickImmediately),
            ScaleDurationWithAbilityTimeRate: Flag(
                nameof(ChannelingTaskData.ScaleDurationWithAbilityTimeRate), Defaults.ScaleDurationWithAbilityTimeRate),
            EnablePartialTicks: Flag(nameof(ChannelingTaskData.EnablePartialTicks), Defaults.EnablePartialTicks),
            AllowSpellPushback: Flag(nameof(ChannelingTaskData.AllowSpellPushback), Defaults.AllowSpellPushback));
    }

    public static bool ScalesWithAttackSpeed(UObject cdo, AbstractFileProvider provider) =>
        ClassDefaults.Inherited(cdo, AttackSpeedField, provider)?.GenericValue is true;

    /// <summary>The ability's <c>AbilityTags</c> in the asset's order, from the nearest class of its chain with them.</summary>
    public static IReadOnlyList<string> AbilityTags(UObject cdo, AbstractFileProvider provider) =>
        PropertyWalk.AsTags(ClassDefaults.Inherited(cdo, AbilityTagsField, provider)?.GenericValue) ?? [];

    public static IReadOnlyList<string> ActivationBlockedTags(UObject cdo, AbstractFileProvider provider) =>
        Tags(cdo, BlockedTagsField, provider);

    /// <summary>
    /// The combined granted tags of the effect's <c>TargetTagsGameplayEffectComponent</c>, from the nearest class of
    /// its chain with the component, and otherwise of the effect's <c>InheritableOwnedTagsContainer</c>.
    /// </summary>
    public static IReadOnlyList<string> GrantedTags(UObject effect, AbstractFileProvider provider)
    {
        var chain = ClassDefaults.Chain(effect, provider).ToArray();

        foreach (var holder in chain)
        {
            foreach (var export in holder.Owner?.GetExports() ?? [])
            {
                if (export.ExportType != TargetTagsComponent) continue;
                if (Combined(export, GrantedContainerField) is { } granted) return Ordered(granted);
            }
        }

        foreach (var holder in chain)
        {
            if (Combined(holder, OwnedContainerField) is { } owned) return Ordered(owned);
        }

        return [];
    }

    /// <summary>The calculation class of each execution, from the nearest class of the chain with <c>Executions</c>.</summary>
    public static IReadOnlyList<string> Executions(UObject effect, AbstractFileProvider provider)
    {
        if (ClassDefaults.Inherited(effect, ExecutionsField, provider)?.GenericValue is not UScriptArray executions) return [];

        var found = new List<string>();

        foreach (var element in executions.Properties)
        {
            if (element?.GenericValue is not FScriptStruct { StructType: IPropertyHolder execution }) continue;
            if (PropertyWalk.GetString(execution, CalculationClassField) is not { Length: > 0 } path) continue;

            var name = path[(path.LastIndexOfAny(['.', '/']) + 1)..];

            if (name.EndsWith(ClassSuffix, StringComparison.Ordinal)) name = name[..^ClassSuffix.Length];
            if (name.Length > 0 && !found.Contains(name)) found.Add(name);
        }

        return found;
    }

    private static string[]? Combined(IPropertyHolder holder, string container) =>
        PropertyWalk.GetStruct(holder, container) is { } inherited
            ? PropertyWalk.GetTags(inherited, CombinedTagsField)
            : null;

    private static IReadOnlyList<string> Tags(UObject cdo, string field, AbstractFileProvider provider) =>
        Ordered(PropertyWalk.AsTags(ClassDefaults.Inherited(cdo, field, provider)?.GenericValue) ?? []);

    private static IReadOnlyList<string> Ordered(IEnumerable<string> tags) =>
        [.. tags.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
}
