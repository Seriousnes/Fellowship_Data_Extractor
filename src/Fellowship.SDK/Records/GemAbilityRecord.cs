namespace Fellowship.SDK.Records;

public sealed record GemAbilityRecord : EntityRecord
{
    public required GemType GemType { get; init; }

    public required int Node { get; init; }

    public required int RequiredPower { get; init; }

    public RecordReference? Replaces { get; init; }

    public IReadOnlyList<RecordReference>? Grants { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public GemOverflowRecord? GemOverflow { get; init; }
}
