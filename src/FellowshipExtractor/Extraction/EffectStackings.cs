using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// How a stacking effect takes a second application: its stacking type and the three policies the effects container
/// reads on a stack (<c>StackDurationRefreshPolicy</c> and <c>StackPeriodResetPolicy</c> at
/// 0x144A6987D-0x144A69890 in the 26162532 shipping exe). Each field is the one the nearest effect in the class chain
/// declares. A field no effect declares is the <c>UGameplayEffect</c> constructor's: RefreshOnSuccessfulApplication,
/// ResetOnSuccessfulApplication and ClearEntireStack. An effect of stacking type None stacks nothing and has none.
/// </summary>
public static class EffectStackings
{
    private const string TypeField = "StackingType";
    private const string DurationRefreshField = "StackDurationRefreshPolicy";
    private const string PeriodResetField = "StackPeriodResetPolicy";
    private const string ExpirationField = "StackExpirationPolicy";
    private const string NoStacking = "None";
    private const string DefaultDurationRefresh = "RefreshOnSuccessfulApplication";
    private const string DefaultPeriodReset = "ResetOnSuccessfulApplication";
    private const string DefaultExpiration = "ClearEntireStack";

    public static EffectStacking? Of(UObject? cdo, AbstractFileProvider provider)
    {
        if (cdo is null) return null;
        if (Member(cdo, TypeField, provider) is not { } type || type == NoStacking) return null;

        return new EffectStacking(type)
        {
            DurationRefresh = Member(cdo, DurationRefreshField, provider) ?? DefaultDurationRefresh,
            PeriodReset = Member(cdo, PeriodResetField, provider) ?? DefaultPeriodReset,
            Expiration = Member(cdo, ExpirationField, provider) ?? DefaultExpiration,
        };
    }

    private static string? Member(UObject cdo, string field, AbstractFileProvider provider) =>
        PropertyWalk.AsString(ClassDefaults.Inherited(cdo, field, provider)?.GenericValue) is { } value
            ? value[(value.LastIndexOf(':') + 1)..]
            : null;
}
