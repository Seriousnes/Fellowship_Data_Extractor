using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class ModifierDocument : EntityDocument
{
    public string? Description { get; init; }

    public string? Icon { get; init; }

    public string? Kind { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public int? MinimumHeroRank { get; init; }
}
