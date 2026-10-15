using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class SetBonusPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}
