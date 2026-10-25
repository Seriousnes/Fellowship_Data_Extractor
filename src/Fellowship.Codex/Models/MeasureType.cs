using System.ComponentModel;

namespace Fellowship.Codex.Models;

public enum MeasureType
{
    [Description("Cooldown")]
    Cooldown,

    [Description("Cast time")]
    Cast,

    [Description("Channel")]
    Channel,

    [Description("Range")]
    Range,

    [Description("Radius")]
    Radius,

    [Description("Duration")]
    Duration,

    [Description("Cost")]
    Cost,

    [Description("Charges")]
    Charges,

    [Description("Max rank")]
    Rank,

    [Description("ID")]
    Id,
}
