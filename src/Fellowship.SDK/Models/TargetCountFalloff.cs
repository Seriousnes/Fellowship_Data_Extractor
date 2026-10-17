namespace Fellowship.SDK.Models;

public sealed record TargetCountFalloff
{
    public EntityReference? Effect { get; init; }

    public double Threshold { get; init; }
}
