using System.Globalization;

namespace Fellowship.Codex.Models;

public sealed class Attunements
{
    public required IReadOnlyList<Attunement> Values { get; init; }

    public static bool TryParse(string? value, out Attunements result)
    {
        var parsed = new List<Attunement>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Attunement.TryParse(part, CultureInfo.InvariantCulture, out var augmentation))
            {
                result = new Attunements { Values = [] };
                return false;
            }

            parsed.Add(augmentation);
        }

        result = new Attunements { Values = parsed };
        return true;
    }
}

public sealed class ItemModifiers
{
    public required IReadOnlyList<ItemModifier> Values { get; init; }

    public static bool TryParse(string? value, out ItemModifiers result)
    {
        var parsed = new List<ItemModifier>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ItemModifier.TryParse(part, CultureInfo.InvariantCulture, out var modifier))
            {
                result = new ItemModifiers { Values = [] };
                return false;
            }

            parsed.Add(modifier);
        }

        result = new ItemModifiers { Values = parsed };
        return true;
    }
}

public sealed record TooltipQuery
{
    public string? Hero { get; init; }

    public string? Rarity { get; init; }

    public string? State { get; init; }

    public int? EquippedTier { get; init; }

    public int? Difficulty { get; init; }

    public int? ItemLevel { get; init; }
    public int? Upgrades { get; init; }
    public int? MaxUpgrades { get; init; }

    public bool NoSocket { get; init; }

    public bool ShowIcon { get; init; }

    public bool Mobile { get; init; }

    public int? Gem { get; init; }

    public decimal? PotentialPower { get; init; }

    public decimal? Armor { get; init; }

    public IReadOnlyList<ItemModifier> Modifiers { get; init; } = [];

    public IReadOnlyList<Attunement> Attunements { get; init; } = [];

    public IReadOnlyList<string> RankSources { get; init; } = [];

    public int? AllocatedAttunements { get; init; }

    public bool? HasAttunements { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<decimal>> Stats { get; init; } =
        new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, decimal> Attributes { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public decimal? Total(string stat) =>
        Stats.TryGetValue(stat, out var values) ? values.Sum() : null;

    public decimal? Attribute(string stat) =>
        Attributes.TryGetValue(stat, out var value) ? value : null;

    public decimal? Haste => Attribute("Haste");

    public decimal? Expertise => Attribute("Expertise");
}
