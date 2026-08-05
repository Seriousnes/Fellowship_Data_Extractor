using Fellowship.SDK;

namespace Fellowship.SDK.Documents;

/// <summary>
/// Something a hero or a creature does. Every field is optional because an ability states the times,
/// costs and distances it has and no others: 18% of them state a cast time, 33% a range.
/// </summary>
public sealed class Ability : EntityDocument
{
    public string? Description { get; init; }
    public IReadOnlyList<string>? Detail { get; init; }
    public string? Icon { get; init; }

    public double? CastTime { get; init; }
    public double? ChannelTime { get; init; }
    public double? ChannelTick { get; init; }
    public double? Cooldown { get; init; }
    public double? Charges { get; init; }
    public double? Cost { get; init; }

    /// <summary>What the cost is paid in: Mana, Spirit, Fury, Burning Ember.</summary>
    public string? Resource { get; init; }

    public double? Range { get; init; }
    public double? Radius { get; init; }
    public double? Duration { get; init; }
    public double? Period { get; init; }

    /// <summary>Set where the ability states that its own times run faster with the wearer's haste.</summary>
    public bool? ScalesWithHaste { get; init; }

    public IReadOnlyList<string>? ScalesOff { get; init; }
    public IReadOnlyList<Placeholder>? Placeholders { get; init; }
    public IReadOnlyList<string>? Schools { get; init; }
}
