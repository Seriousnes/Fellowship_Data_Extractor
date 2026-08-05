namespace Fellowship.SDK.Documents;

/// <summary>One of the ten rungs on a gem type's ladder, unlocked by accumulated power.</summary>
public sealed class GemTrait : EntityDocument
{
    public required GemTypeName Gem { get; init; }

    /// <summary>Where on its type's ten-step ladder this sits, 1 to 10.</summary>
    public required int Node { get; init; }

    public required int RequiredPower { get; init; }

    /// <summary>The weaker node this takes the place of, on the upper half of every ladder.</summary>
    public int? ReplacesId { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
