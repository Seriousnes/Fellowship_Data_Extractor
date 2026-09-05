using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public sealed record EffectRecord : EntityRecord
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
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public RecordReference? NameBorrowedFrom { get; init; }
    public RecordReference? ArtBorrowedFrom { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public RecordReference? Owner { get; init; }

    public RecordReference? AppliedBy { get; init; }

    public RecordReference? PartOf { get; init; }

    public string? Role { get; init; }
}
