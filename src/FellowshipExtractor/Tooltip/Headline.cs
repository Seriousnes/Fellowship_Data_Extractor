namespace FellowshipExtractor.Tooltip;

/// <summary>
/// The numbers a tooltip puts above the description. Each is a curve binding picked out by the
/// field that owns it, so it works for any ability without naming heroes or tables.
/// </summary>
public static class Headline
{
    private static readonly (string Field, string Property)[] Fields =
    [
        ("castTime", "CastingData.CastingDuration"),
        ("channelTime", "ChannelingData.ChannelingDuration"),
        ("cooldown", "CooldownDuration"),
        ("interruptCooldown", "InterruptCooldownDuration"),
        ("radius", "RadiusRef"),
        ("rangeHorizontal", "RangeHorizontal"),
        ("duration", "DurationMagnitude"),
        ("period", "Period"),
    ];

    public static Dictionary<string, double>? From(IReadOnlyList<CurveBinding> bindings)
    {
        var headline = new Dictionary<string, double>();

        foreach (var (field, property) in Fields)
        {
            var match = bindings.FirstOrDefault(b =>
                b.Property.Equals(property, StringComparison.OrdinalIgnoreCase) && b.Value is not null);
            if (match?.Value is { } value) headline[field] = value;
        }

        return headline.Count == 0 ? null : headline;
    }
}
