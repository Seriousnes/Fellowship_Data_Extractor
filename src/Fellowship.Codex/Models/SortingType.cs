using System.ComponentModel;

namespace Fellowship.Codex.Models;

public enum SortingType
{
    [Description("Name")]
    Name,

    [Description("Type")]
    Type,

    [Description("Slot")]
    Slot,

    [Description("Item level")]
    Ilvl,

    [Description("Type")]
    Kind,

    [Description("Primary stat")]
    Stat,

    [Description("Source")]
    Source,

    [Description("Location")]
    Location,

    [Description("Rank")]
    Rank,

    [Description("Health")]
    Health,

    [Description("Qualifier")]
    Qualifier,

    [Description("Required score")]
    Score,

    [Description("Hero")]
    Hero,

    [Description("School")]
    School,

    [Description("Applied by")]
    Applier,

    [Description("Gem type")]
    Gem,

    [Description("Power required")]
    Power,

    [Description("Tier")]
    Tier,

    [Description("Cost")]
    Cost,

    [Description("Rarity")]
    Rarity,

    [Description("Category")]
    Category,
}
