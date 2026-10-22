namespace Fellowship.SDK.Models;

public sealed record CurvePoint(double Time, double Value);

public sealed record Curve(string Interpolation, IReadOnlyList<CurvePoint> Points);

public sealed record TraitStat(string Attribute, double Coefficient)
{
    public Curve? Lookup { get; init; }
}
