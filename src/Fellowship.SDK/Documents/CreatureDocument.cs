namespace Fellowship.SDK.Documents;

public sealed class CreatureDocument : EntityDocument
{
    public string? Icon { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }
}
