using Fellowship.SDK.Models;

namespace Fellowship.Codex.Models;

public static class CurveReading
{
    public const string Constant = "RCIM_Constant";

    public const string Linear = "RCIM_Linear";

    public static double At(Curve curve, double time)
    {
        if (curve.Points.Count == 0)
        {
            throw new InvalidOperationException("A curve with no keys has no value to read.");
        }

        var points = curve.Points.OrderBy(point => point.Time).ToArray();

        if (time <= points[0].Time) return points[0].Value;
        if (time >= points[^1].Time) return points[^1].Value;

        var after = Array.FindIndex(points, point => point.Time > time);
        var before = points[after - 1];
        var next = points[after];

        return curve.Interpolation switch
        {
            Constant => before.Value,
            Linear => before.Value + (next.Value - before.Value) * (time - before.Time) / (next.Time - before.Time),
            _ => throw new InvalidOperationException(
                $"The codex reads {Constant} and {Linear} curves, and this curve interpolates by " +
                $"{curve.Interpolation}."),
        };
    }
}
