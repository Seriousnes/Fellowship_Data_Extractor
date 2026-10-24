namespace Fellowship.SDK.Models;

public sealed record EffectTrigger(string Event)
{
    public bool? PeriodicExecutions { get; init; }

    public bool? TriggerOnce { get; init; }

    public IReadOnlyList<string>? AssetTagsRequired { get; init; }

    public IReadOnlyList<string>? AssetTagsIgnored { get; init; }

    public IReadOnlyList<string>? SourceTagsRequired { get; init; }

    public IReadOnlyList<string>? SourceTagsIgnored { get; init; }

    public IReadOnlyList<string>? TargetTagsRequired { get; init; }

    public IReadOnlyList<string>? TargetTagsIgnored { get; init; }
}
