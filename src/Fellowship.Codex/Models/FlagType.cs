using System.ComponentModel;

namespace Fellowship.Codex.Models;

public enum FlagType
{
    [Description("Show unobtainable entries")]
    Unobtainable,

    [Description("Has texture")]
    Icon,

    [Description("Has description text")]
    Described,

    [Description("Is legendary")]
    Legendary,

    [Description("Grants an ability")]
    Grants,

    [Description("Rolls with a gem socket")]
    Socket,

    [Description("Unique equipped")]
    Unique,
}
