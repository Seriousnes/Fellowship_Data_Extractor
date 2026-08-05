namespace Fellowship.SDK.Documents;

/// <summary>Something an ability applies. Anonymous in the build unless an applier names it.</summary>
public sealed class Effect : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }

    public double? Duration { get; init; }
    public double? Period { get; init; }
    public double? Cooldown { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    /// <summary>Set where the effect keeps its period fixed however much haste the wearer holds.</summary>
    public bool? TickrateIgnoresHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public EntityReference? NameBorrowedFrom { get; init; }
    public EntityReference? ArtBorrowedFrom { get; init; }
}
