namespace Fellowship.SDK.Documents;

public sealed class TalentSlotDocument : EntityDocument
{
    public required Hero Hero { get; init; }

    public required int Row { get; init; }

    public required int Column { get; init; }

    public required int Cost { get; init; }

    public required int TalentId { get; init; }
}
