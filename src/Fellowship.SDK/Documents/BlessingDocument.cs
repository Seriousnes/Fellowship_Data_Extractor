using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class BlessingDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? ScalesOff { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public int? MaxRank { get; init; }

    public IReadOnlyList<string>? Slots { get; init; }

    public int? AbilityRank { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<double?>>? Constants { get; init; }
}
