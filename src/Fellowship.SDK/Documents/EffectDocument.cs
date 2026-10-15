using Fellowship.SDK.Models;

namespace Fellowship.SDK.Documents;

public sealed class EffectDocument : EntityDocument
{
    public string? Description { get; init; }
    public string? Icon { get; init; }

    public double? Duration { get; init; }
    public double? Period { get; init; }
    public double? Cooldown { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    public bool? TickrateIgnoresHaste { get; init; }

    public int? MaxStacks { get; init; }

    public bool? Infinite { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    public IReadOnlyList<ResourceGain>? Generates { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public EntityReference? NameBorrowedFrom { get; init; }
    public EntityReference? TextureBorrowedFrom { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public EntityReference? Owner { get; init; }

    public EntityReference? AppliedBy { get; init; }

    public EntityReference? PartOf { get; init; }

    public string? Role { get; init; }
}
