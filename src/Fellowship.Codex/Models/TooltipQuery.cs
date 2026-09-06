using System.Globalization;

using Fellowship.SDK;

namespace Fellowship.Codex.Models;

public sealed class ImbuedTraits
{
    public required IReadOnlyList<ImbuedTrait> Values { get; init; }

    public static bool TryParse(string? value, out ImbuedTraits result)
    {
        var parsed = new List<ImbuedTrait>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!ImbuedTrait.TryParse(part, CultureInfo.InvariantCulture, out var trait))
            {
                result = new ImbuedTraits { Values = [] };
                return false;
            }

            parsed.Add(trait);
        }

        result = new ImbuedTraits { Values = parsed };
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

public sealed class RolledStats
{
    public required IReadOnlyList<string> Values { get; init; }

    public static bool TryParse(string? value, out RolledStats result)
    {
        result = new RolledStats
        {
            Values = [.. (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
        };

        return true;
    }
}

public readonly record struct Switch(bool On)
{
    private static readonly IReadOnlySet<string> Yes = new HashSet<string>(["", "true", "1", "yes", "on"], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> No = new HashSet<string>(["false", "0", "no", "off"], StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? value, IFormatProvider? provider, out Switch result)
    {
        var given = value?.Trim() ?? string.Empty;

        if (Yes.Contains(given))
        {
            result = new Switch(true);
            return true;
        }

        result = default;
        return No.Contains(given);
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

    public IReadOnlyList<ImbuedTrait> ImbuedTraits { get; init; } = [];

    public int? AllocatedTraits { get; init; }

    public bool? HasImbuedTraits { get; init; }

    public IReadOnlyList<string> RandomStats { get; init; } = [];

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
