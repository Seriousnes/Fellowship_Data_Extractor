namespace Fellowship.SDK.Records;

public sealed record TalentSlotRecord : EntityRecord
{
    public required Hero Hero { get; init; }

    public required int Row { get; init; }

    public required int Column { get; init; }

    public required int Cost { get; init; }

    public required RecordReference Talent { get; init; }
}
