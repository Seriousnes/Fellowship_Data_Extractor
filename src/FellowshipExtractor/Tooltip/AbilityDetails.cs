using System.Text.Json;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Everything the game shows for one ability: its presentation text, its icon, and the tuning
/// constants behind every number in that text. An item's granted ability, a hero's skill and a
/// relic power all reduce to this, so the resolution runs once and is reused wherever an ability
/// is reached.
/// </summary>
public sealed record AbilityDetails(string Asset)
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }
    public TextureExport? IconImage { get; init; }
    public string? PresentationTable { get; init; }
    public string? PresentationRow { get; init; }
    public string? MatchedBy { get; init; }
    public Targeting? Targeting { get; init; }

    /// <summary>Effects the ability applies, each with the attributes it scales off.</summary>
    public IReadOnlyList<EffectRef>? Effects { get; init; }

    public IReadOnlyList<ResolvedToken>? DescriptionTokens { get; init; }
    public IReadOnlyList<CurveBinding>? Constants { get; init; }
    public JsonElement? Properties { get; init; }
    public string? Unresolved { get; init; }
}
