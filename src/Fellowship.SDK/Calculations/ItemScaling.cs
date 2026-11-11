using Fellowship.SDK.Models;

namespace Fellowship.SDK.Calculations;

/// <summary>Item level, tempering, rarity and crafted rows an item's stats are read at.</summary>
public sealed record ItemScaling
{
    /// <summary>Hero wearing the item. An item with one reading per hero is read for this hero.</summary>
    public string? Hero { get; init; }

    /// <summary>Item level. With none, the item is read at its drop item level.</summary>
    public int? ItemLevel { get; init; }

    /// <summary>Tempers applied.</summary>
    public int? Upgrades { get; init; }

    /// <summary>Tempering ceiling. With none, the ceiling is the rarity's at the item level.</summary>
    public int? MaxUpgrades { get; init; }

    /// <summary>Rarity tier number or display name. With none, the item's highest rarity.</summary>
    public string? Rarity { get; init; }

    /// <summary>The gem socket scores no potential power.</summary>
    public bool NoSocket { get; init; }

    /// <summary>Armor in place of the computed armor.</summary>
    public decimal? Armor { get; init; }

    /// <summary>Modifiers in the item's crafted rows. An enchant among them adds its stats.</summary>
    public IReadOnlyList<ItemModifier> Modifiers { get; init; } = [];

    /// <summary>Augmentations attuned in the item's attunement tree. An enchant among them adds its stats.</summary>
    public IReadOnlyList<Attunement> Attunements { get; init; } = [];

    /// <summary>Stat values by stat key in place of the computed rows. A value past the item's declared rows is a rolled row.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<decimal>> Stats { get; init; } =
        new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
}
