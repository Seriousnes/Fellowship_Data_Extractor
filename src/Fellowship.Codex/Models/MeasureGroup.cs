using System.ComponentModel;

namespace Fellowship.Codex.Models;

/// <summary>Heading of a measure in the rail's additional filters.</summary>
public enum MeasureGroup
{
    /// <summary>Armor and the stats on the primary pool: Stamina and the primary attributes.</summary>
    [Description("Base stats")]
    Base,

    /// <summary>The item stats on the secondary pool.</summary>
    [Description("Secondary stats")]
    Secondary,

    [Description("Stats")]
    Stats,
}
