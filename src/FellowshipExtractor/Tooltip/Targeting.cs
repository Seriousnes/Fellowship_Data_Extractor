using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// What the asset says about who an ability can be used on. The shipped data spreads this over
/// gameplay tags, a targeting ability and a selection policy rather than one display field, so
/// each part is reported as it stands instead of being collapsed into a single label.
/// </summary>
public sealed record Targeting
{
    public string[]? AbilityTags { get; init; }
    public string? TargetingAbility { get; init; }
    public string? TargetAcquisitionPolicy { get; init; }
    public string? CustomTargetingAbilityTag { get; init; }

    /// <summary>Enum ordinal. The usmap carries no enum table yet, so it has no name.</summary>
    public string? AbilityCategoryOrdinal { get; init; }

    public bool ActivationRequiresValidTarget { get; init; }

    public static Targeting? From(UObject cdo)
    {
        var targeting = new Targeting
        {
            AbilityTags = PropertyWalk.GetTags(cdo, "AbilityTags"),
            TargetingAbility = PropertyWalk.GetString(cdo, "TargetingAbility"),
            TargetAcquisitionPolicy = PropertyWalk.GetString(cdo, "TargetAcquisitionPolicy"),
            CustomTargetingAbilityTag = PropertyWalk.GetString(cdo, "CustomTargetingAbilityTag"),
            AbilityCategoryOrdinal = PropertyWalk.GetString(cdo, "AbilityCategory"),
            ActivationRequiresValidTarget =
                PropertyWalk.Get(cdo, "ActivationRequiresValidTarget")?.GenericValue is true,
        };

        return targeting.AbilityTags is null
            && targeting.TargetingAbility is null
            && targeting.TargetAcquisitionPolicy is null
            && targeting.AbilityCategoryOrdinal is null
            ? null
            : targeting;
    }
}
