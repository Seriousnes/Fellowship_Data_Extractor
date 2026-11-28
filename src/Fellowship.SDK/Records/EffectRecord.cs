using Fellowship.SDK.Models;

namespace Fellowship.SDK.Records;

public sealed record EffectRecord : EntityRecord
{
    public IReadOnlyList<ResourceGainRecord>? Generates { get; init; }

    public IReadOnlyList<string>? EffectTags { get; init; }

    public string? Description { get; init; }
    public string? Icon { get; init; }

    public double? Duration { get; init; }
    public DurationScaling? DurationScaling { get; init; }
    public double? Period { get; init; }
    public double? Cooldown { get; init; }
    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Cost { get; init; }

    public bool? TickrateIgnoresHaste { get; init; }

    public int? MaxStacks { get; init; }

    public bool? Infinite { get; init; }

    public string? DurationPolicy { get; init; }

    public string? MaxHealthModificationPolicy { get; init; }

    public string? MaxHealthOnRemovedPolicy { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<PlaceholderRecord>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }

    public IReadOnlyList<EffectModifier>? Modifiers { get; init; }

    public IReadOnlyList<CalculationModifier>? CalculationModifiers { get; init; }

    public string? CalculationPreset { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? Talents { get; init; }

    public RecordReference? NameBorrowedFrom { get; init; }
    public RecordReference? TextureBorrowedFrom { get; init; }

    public IReadOnlyList<Hero>? Heroes { get; init; }

    public RecordReference? Owner { get; init; }

    public RecordReference? AppliedBy { get; init; }

    public string? VisualDisplayName { get; init; }

    public bool? AlwaysUseVisualDisplayName { get; init; }

    public string? LogIdClass { get; init; }

    public RecordReference? LoggedUnder { get; init; }

    public bool? LogsEffectEvents { get; init; }

    public IReadOnlyList<EffectLinkRecord>? LinkedBy { get; init; }

    public IReadOnlyList<CommitTriggerRecord>? AppliedOnCommitOf { get; init; }

    public RecordReference? PartOf { get; init; }

    public string? Role { get; init; }
}

public sealed record EffectLinkRecord(RecordReference Parent, string ApplyTo)
{
    public RecordReference? Talent { get; init; }
}
