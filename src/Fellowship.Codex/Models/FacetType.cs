using System.ComponentModel;

namespace Fellowship.Codex.Models;

public enum FacetType
{
    [Description("Slot")]
    Slot,

    [Description("Item type")]
    Kind,

    [Description("Armour")]
    Armor,

    [Description("Primary stat")]
    Stat,

    [Description("Hero")]
    Hero,

    [Description("Source")]
    Source,

    [Description("Rank")]
    Rank,

    [Description("School")]
    School,

    [Description("Resource")]
    Resource,

    [Description("Gem type")]
    Gem,

    [Description("Rarity")]
    Rarity,
}
