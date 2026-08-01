using System.Text.Json;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// A character augmentation: the ability an item grants, or one of a legendary's bonus stats.
/// Both are CRCharacterAugmentationAsset, so the same reader serves them.
///
/// A legendary's bonus stat carries its own name and line. An item's granted ability usually
/// carries neither and names a gameplay ability instead, whose presentation row holds the text.
/// Reading only the augmentation's own fields therefore returns an empty result for exactly the
/// abilities that have the most to show, so the abilities it grants are resolved too.
/// </summary>
public sealed record Augmentation(string Asset)
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<AbilityDetails>? Abilities { get; init; }
    public string[]? GrantedGameplayTags { get; init; }
    public JsonElement? Properties { get; init; }
    public string? Unresolved { get; init; }

    /// <summary>The name and text to show, taken from the ability when the asset carries none.</summary>
    public string? EffectiveName => DisplayName ?? Abilities?.Select(a => a.DisplayName).FirstOrDefault(n => n is not null);

    public string? EffectiveDescription =>
        Description ?? Abilities?.Select(a => a.Description).FirstOrDefault(d => d is not null);
}
