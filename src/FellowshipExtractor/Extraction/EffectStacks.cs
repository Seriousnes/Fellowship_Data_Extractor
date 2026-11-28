using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The stack members of a gameplay effect with a <c>StackingType</c> other than <c>None</c>. Each policy is the
/// value the effect or a Blueprint parent states, otherwise the value <c>UGameplayEffect</c>'s constructor stores.
/// </summary>
public sealed record EffectStack(
    string StackingType,
    string DurationRefreshPolicy,
    string PeriodResetPolicy,
    string ExpirationPolicy,
    bool DenyOverflowApplication,
    bool ClearStackOnOverflow,
    IReadOnlyList<string> OverflowEffects);

/// <summary>
/// The stack rule of a gameplay effect. Shipping exe 26320967.
/// <para>
/// <c>FActiveGameplayEffectsContainer::ApplyGameplayEffectSpec</c> 0x144A6A660 looks the applied spec's stack up
/// (0x144A74BD0): an effect with no <c>StackingType</c> (<c>UGameplayEffect</c> +0x9E1), or with the
/// <c>Instant</c> policy, has none, and each application is an active effect of its own. <c>AggregateByTarget</c>
/// has one stack on a target, and <c>AggregateBySource</c> one for each instigator component (0x144A74DC7 to
/// 0x144A74E0C).
/// </para>
/// <para>
/// An application on a stack has the count <c>min(existing + applied, StackCountMax)</c>, with no cap for a
/// <c>StackCountMax</c> of 0 or less (0x144A6A78F to 0x144A6A7AD). The spec's <c>StackCountMax</c> (+0x58) is the
/// effect's <c>StackCountMax</c> scalable float (+0x9E8) rounded at the spec's level (0x144A8F520 to 0x144A8F550).
/// No routine reads <c>StackLimitCount</c> (+0x9E4). The new spec is assigned over the stack's spec (0x144A6A93B),
/// so its SetByCaller magnitudes and its captured attributes replace the old ones for every stack.
/// </para>
/// <para>
/// A stack at its cap goes through the overflow routine 0x144A7C3F0 first: it applies each of
/// <c>OverflowEffects</c> (+0x2B0), and for <c>bDenyOverflowApplication</c> (+0x2C0) the application ends there,
/// with the stack removed for <c>bClearStackOnOverflow</c> (+0x2C1). With no denial the application replaces the
/// spec and goes on as below.
/// </para>
/// <para>
/// <c>StackDurationRefreshPolicy</c> (+0xA11) other than <c>NeverRefresh</c> restarts the duration (0x144A6A99D), and
/// <c>StackPeriodResetPolicy</c> (+0xA12) other than <c>NeverReset</c> sets the period timer again at one full period
/// (0x144A6A9A9, 0x144A6B2F4 to 0x144A6B518). <c>StackExpirationPolicy</c> (+0xA13) is read at the end of the
/// duration (0x144A6FC35): <c>ClearEntireStack</c> removes the effect, <c>RemoveSingleStackAndRefreshDuration</c>
/// removes one stack and restarts the duration, and <c>RefreshDuration</c> restarts the duration with every stack.
/// </para>
/// <para>
/// The constructor 0x144A63AB0 stores 0 at <c>StackingType</c> (0x144A63EEE), at
/// <c>RecaptureSourceAttributesBeforeEveryExecution</c> (+0xA10, 0x144A63E60), and at the duration and period
/// policies as one word (0x144A63F2B). It has no store at +0xA13, +0x2C0 or +0x2C1, and the allocation is zero filled.
/// <c>CRGameplayEffectDamage</c>'s constructor 0x1450E84D0 stores none of them, and sets bits 1 and 3 of the byte
/// with <c>bSilentStack</c> in bit 0 (+0xA98, 0x1450E851E).
/// </para>
/// <para>
/// <c>CRChargeEffect</c>'s constructor 0x1450E71E0 is the one other routine of the executable with a store at
/// +0x9E1 or +0xA13: <c>AggregateByTarget</c> (0x1450E749D), <c>NeverRefresh</c> and <c>NeverReset</c> as one
/// word (0x1450E74D3), and <c>RemoveSingleStackAndRefreshDuration</c> (0x1450E74DC). It assigns a
/// <c>StackCountMax</c> of 0 (0x1450E74CA).
/// </para>
/// </summary>
public static class EffectStacks
{
    private const string TypeField = "StackingType";
    private const string TypeEnum = "EGameplayEffectStackingType";
    private const string DurationField = "StackDurationRefreshPolicy";
    private const string DurationEnum = "EGameplayEffectStackingDurationPolicy";
    private const string PeriodField = "StackPeriodResetPolicy";
    private const string PeriodEnum = "EGameplayEffectStackingPeriodPolicy";
    private const string ExpirationField = "StackExpirationPolicy";
    private const string ExpirationEnum = "EGameplayEffectStackingExpirationPolicy";
    private const string DenyField = "bDenyOverflowApplication";
    private const string ClearField = "bClearStackOnOverflow";
    private const string OverflowField = "OverflowEffects";
    private const string SilentField = "bSilentStack";
    private const string RecaptureField = "RecaptureSourceAttributesBeforeEveryExecution";
    private const string ClassSuffix = "_C";

    /// <summary>The ordinals a native class's constructor stores at the four enum members.</summary>
    private sealed record Stores(long Type, long Duration, long Period, long Expiration);

    /// <summary><c>UGameplayEffect</c>'s constructor, and each native class with no store of its own.</summary>
    private static readonly Stores Zero = new(0, 0, 0, 0);

    private static readonly IReadOnlyDictionary<string, Stores> ConstructorStores =
        new Dictionary<string, Stores>(StringComparer.Ordinal)
        {
            ["CRChargeEffect"] = new(Type: 2, Duration: 1, Period: 1, Expiration: 1),   // 0x1450E749D, 0x1450E74D3, 0x1450E74DC
        };

    /// <summary>
    /// Stack members of <paramref name="effect"/>, or null for an effect with the <c>StackingType</c> <c>None</c>. The
    /// caller leaves an effect of the <c>Instant</c> policy without them.
    /// </summary>
    public static EffectStack? Of(UObject effect, AbstractFileProvider provider, TypeMappings mappings)
    {
        if (StackingType(effect, provider, mappings) is not { } type) return null;

        var stores = StoresOf(effect, provider);

        return new EffectStack(
            type,
            Member(effect, DurationField, DurationEnum, stores.Duration, provider, mappings),
            Member(effect, PeriodField, PeriodEnum, stores.Period, provider, mappings),
            Member(effect, ExpirationField, ExpirationEnum, stores.Expiration, provider, mappings),
            True(effect, DenyField, provider),
            True(effect, ClearField, provider),
            Classes(effect, OverflowField, provider));
    }

    /// <summary>The <c>StackingType</c> of <paramref name="effect"/>, or null for <c>None</c>.</summary>
    public static string? StackingType(UObject effect, AbstractFileProvider provider, TypeMappings mappings)
    {
        var type = Member(effect, TypeField, TypeEnum, StoresOf(effect, provider).Type, provider, mappings);

        return type == Stored(TypeEnum, Zero.Type, mappings) ? null : type;
    }

    /// <summary>
    /// True for an effect with <c>bSilentStack</c>, a member of <c>CRGameplayEffectDamage</c>. The damage
    /// calculation's gather takes <c>min(stack count, 1)</c> for it (0x1450DB5BE to 0x1450DB5D1), so its hit has no
    /// stack count term.
    /// </summary>
    public static bool SilentStack(UObject effect, AbstractFileProvider provider) => True(effect, SilentField, provider);

    /// <summary>
    /// True for an effect with <c>RecaptureSourceAttributesBeforeEveryExecution</c>. Each execution of it, a periodic
    /// tick (0x144A808C4) and the execution of an instant application (0x1449FD735), clears the spec's source capture
    /// flag and captures the source's attributes again (0x144A6F7D0).
    /// </summary>
    public static bool Recaptures(UObject effect, AbstractFileProvider provider) => True(effect, RecaptureField, provider);

    private static Stores StoresOf(UObject effect, AbstractFileProvider provider)
    {
        var native = ClassDefaults.NativeParent(effect, provider);
        var name = native?[(native.LastIndexOf('.') + 1)..];

        return name is not null && ConstructorStores.TryGetValue(name, out var stores) ? stores : Zero;
    }

    private static string Member(
        UObject effect, string field, string enumName, long stored, AbstractFileProvider provider, TypeMappings mappings)
    {
        var stated = PropertyWalk.AsString(ClassDefaults.Inherited(effect, field, provider)?.GenericValue);

        if (stated is null) return Stored(enumName, stored, mappings);

        return EnumOrdinals.Member(mappings, enumName, stated)
            ?? throw new InvalidOperationException(
                $"{effect.GetPathName()} states {field} = {stated}, and {enumName} has no such member in the mappings.");
    }

    private static string Stored(string enumName, long ordinal, TypeMappings mappings) =>
        EnumOrdinals.Members(mappings, enumName).TryGetValue(ordinal, out var member)
            ? member
            : throw new InvalidOperationException($"{enumName} has no member with the ordinal {ordinal}.");

    private static bool True(UObject effect, string field, AbstractFileProvider provider) =>
        ClassDefaults.Inherited(effect, field, provider)?.GenericValue is true;

    private static IReadOnlyList<string> Classes(UObject effect, string field, AbstractFileProvider provider)
    {
        if (ClassDefaults.Inherited(effect, field, provider)?.GenericValue is not UScriptArray listed) return [];

        return [.. listed.Properties
            .Select(entry => PropertyWalk.AsString(entry?.GenericValue))
            .OfType<string>()
            .Select(AssetName)];
    }

    private static string AssetName(string classPath)
    {
        var name = classPath[(classPath.LastIndexOfAny(['.', '/']) + 1)..];

        return name.EndsWith(ClassSuffix, StringComparison.Ordinal) ? name[..^ClassSuffix.Length] : name;
    }
}
