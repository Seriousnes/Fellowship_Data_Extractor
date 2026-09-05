namespace Fellowship.SDK.Records;

public sealed record CompanionPetRecord : EntityRecord
{
    public string? Description { get; init; }

    public string? Acquire { get; init; }

    public string? Icon { get; init; }
}
