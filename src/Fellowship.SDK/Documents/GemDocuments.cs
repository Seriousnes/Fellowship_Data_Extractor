using Fellowship.SDK;

namespace Fellowship.SDK.Documents;

/// <summary>
/// One of the six gem types. A socketed gem adds its tier's power to this type's running total, and
/// the type's ten-node ladder unlocks against that total.
/// </summary>
public sealed class GemType : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    /// <summary>The attribute this type accumulates into.</summary>
    public required string Attribute { get; init; }

    public string? Icon { get; init; }

    /// <summary>What the type keeps giving past the top of its ladder.</summary>
    public string? OverflowDescription { get; init; }

    public IReadOnlyList<Placeholder>? OverflowPlaceholders { get; init; }
}
