using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class TraitPowerDocument : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? DescriptionsByHero { get; init; }
    public IReadOnlyDictionary<string, string>? NameByHero { get; init; }
    public string? Icon { get; init; }
    public double? Cooldown { get; init; }
    public double? Duration { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<EntityReference>? Grants { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }
}
