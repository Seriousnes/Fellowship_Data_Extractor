using Fellowship.SDK;

namespace Fellowship.SDK.Documents;

/// <summary>
/// A hero's talent. 215 of the 216 live in an augmentation a slot points at; Macabre Stratagem
/// lives on the slot row itself, Mara's row 4 column 1.
/// </summary>
public sealed class Talent : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }
}
