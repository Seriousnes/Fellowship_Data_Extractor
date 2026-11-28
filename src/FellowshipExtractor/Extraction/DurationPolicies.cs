using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// A gameplay effect's <c>DurationPolicy</c>: the value the effect or a Blueprint parent states, otherwise the value
/// the constructor of its native class stores. The cooked default object of an effect omits a field equal to its
/// parent's, and no shipped file has a native class's default object.
/// </summary>
public static class DurationPolicies
{
    public const string Infinite = "Infinite";
    public const string HasDuration = "HasDuration";

    private const string Field = "DurationPolicy";
    private const string EnumName = "EGameplayEffectDurationType";

    /// <summary>
    /// The ordinal each native effect class's constructor stores at <c>UGameplayEffect::DurationPolicy</c> (+0x30).
    /// Shipping exe 26320967. <c>UGameplayEffect</c>'s constructor 0x144A63AB0 stores 0 at 0x144A63ED7, and each
    /// class below calls it before its own store. <c>CRGameplayEffectDamage</c>'s constructor 0x1450E84D0 has no
    /// store of its own.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, long> ConstructorStores = new Dictionary<string, long>(StringComparer.Ordinal)
    {
        ["GameplayEffect"] = 0,                          // 0x144A63ED7
        ["CRGameplayEffectDamage"] = 0,                  // constructor 0x1450E84D0
        ["CRAbsorptionEffect"] = 1,                      // 0x1450E6804
        ["CRCastingGameplayEffect"] = 2,                 // 0x1450E6A2F
        ["CRChannelingGameplayEffect"] = 2,              // 0x1450E70C2
        ["CRChargeEffect"] = 2,                          // 0x1450E7211
        ["CRCostEffect"] = 0,                            // 0x1450E7704
        ["CRDamageReflectGameplayEffect"] = 0,           // 0x1450E78B5
        ["CRDisconnectedGameplayEffect"] = 1,            // 0x1450E7E62
        ["CRGameplayEffectActorDependency"] = 1,         // 0x1450E84B5
        ["CRGenericAbilityCooldownEffect"] = 2,          // 0x1450E8584
        ["CRGodModeEffect"] = 1,                         // 0x1450E8739
        ["CRInfiniteChannelingGameplayEffect"] = 1,      // 0x1450E8C15
        ["CRInterruptGameplayEffect"] = 0,               // 0x1450E8C64
        ["CRKnockbackEffect"] = 0,                       // 0x1450E8E24
        ["CRLifeStealGameplayEffect"] = 0,               // 0x1450E90CD
        ["CRLockoutGameplayEffect"] = 2,                 // 0x1450E953D
        ["CRProcEffect"] = 1,                            // 0x1450E9864
        ["CRReflectHealGameplayEffect"] = 0,             // 0x1450E9B4D
        ["CRSetByCallerDamageGameplayEffect"] = 0,       // 0x1450E9FA0
        ["CRSetByCallerHealGameplayEffect"] = 0,         // 0x1450EA380
    };

    public static string Of(UObject cdo, AbstractFileProvider provider, TypeMappings mappings)
    {
        var stated = PropertyWalk.AsString(ClassDefaults.Inherited(cdo, Field, provider)?.GenericValue);

        if (EnumOrdinals.Member(mappings, EnumName, stated) is { } member) return member;

        var native = ClassDefaults.NativeParent(cdo, provider);
        var name = native?[(native.LastIndexOf('.') + 1)..];

        if (name is null || !ConstructorStores.TryGetValue(name, out var ordinal))
        {
            throw new InvalidOperationException(
                $"{cdo.Name} states no {Field} and its native class {native ?? "(none)"} has no constructor store " +
                $"recorded in {nameof(DurationPolicies)}. Decode the class's constructor in the shipping exe and add " +
                "the byte it stores at +0x30.");
        }

        return EnumOrdinals.Members(mappings, EnumName).TryGetValue(ordinal, out var declared)
            ? declared
            : throw new InvalidOperationException($"{EnumName} has no member with the ordinal {ordinal}.");
    }
}
