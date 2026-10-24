namespace Fellowship.SDK.Models;

public sealed record ProcRoll(string Stream)
{
    public double? ProcsPerMinute { get; init; }

    public IReadOnlyList<double?>? ProcsPerMinuteByRank { get; init; }

    public bool? ScalesWithHaste { get; init; }
}
