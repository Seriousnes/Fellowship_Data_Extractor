namespace Fellowship.SDK.Documents;

/// <summary>
/// One position in one hero's tree. 18 for each of the 12 heroes, each naming exactly one hero and
/// exactly one talent, which is why every field here is required.
/// </summary>
public sealed class TalentSlot : EntityDocument
{
    public required Hero Hero { get; init; }

    /// <summary>Where it sits in the grid, 1 to 6 down and 1 to 3 across.</summary>
    public required int Row { get; init; }

    public required int Column { get; init; }

    /// <summary>What it costs to take, 1 to 3.</summary>
    public required int Cost { get; init; }

    public required int TalentId { get; init; }
}
