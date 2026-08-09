using System.Globalization;

namespace FellowshipExtractor.Api.Models;

public readonly record struct GemPower(int GemTypeId, int Power)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out GemPower result)
    {
        result = default;

        var separator = value?.IndexOf(':') ?? -1;
        if (value is null || separator <= 0) return false;

        if (!int.TryParse(value.AsSpan(..separator), NumberStyles.Integer, provider, out var type)) return false;
        if (!int.TryParse(value.AsSpan((separator + 1)..), NumberStyles.Integer, provider, out var power)) return false;

        result = new GemPower(type, power);
        return true;
    }
}

public readonly record struct ImbuedTrait(int Id, int Rank)
{
    public static bool TryParse(string? value, IFormatProvider? provider, out ImbuedTrait result)
    {
        result = default;

        var separator = value?.IndexOf(':') ?? -1;
        if (value is null) return false;

        var netId = separator < 0 ? value : value[..separator];

        if (!int.TryParse(netId, NumberStyles.Integer, provider, out var trait)) return false;

        var rank = 1;
        if (separator >= 0 &&
            !int.TryParse(value.AsSpan((separator + 1)..), NumberStyles.Integer, provider, out rank))
        {
            return false;
        }

        result = new ImbuedTrait(trait, rank);
        return true;
    }
}

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

public sealed class GemPowers
{
    public required IReadOnlyList<GemPower> Values { get; init; }

    public static bool TryParse(string? value, out GemPowers result)
    {
        var parsed = new List<GemPower>();

        foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!GemPower.TryParse(part, CultureInfo.InvariantCulture, out var gem))
            {
                result = new GemPowers { Values = [] };
                return false;
            }

            parsed.Add(gem);
        }

        result = new GemPowers { Values = parsed };
        return true;
    }
}

public sealed record TooltipQuery
{
    public string? Hero { get; init; }

    public string? Rarity { get; init; }

    public string? State { get; init; }

    public int? EquippedTier { get; init; }

    public int? ItemLevel { get; init; }
    public int? Upgrades { get; init; }
    public int? MaxUpgrades { get; init; }

    public bool NoSocket { get; init; }

    public decimal? PotentialPower { get; init; }

    public decimal? Armor { get; init; }

    public IReadOnlyList<ImbuedTrait> Traits { get; init; } = [];

    public int? AllocatedTraits { get; init; }
    public int? MaxTraits { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<decimal>> Stats { get; init; } =
        new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, decimal> Attributes { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GemPower> GemPowers { get; init; } = [];

    public decimal? Total(string stat) =>
        Stats.TryGetValue(stat, out var values) ? values.Sum() : null;

    public decimal? Attribute(string stat) =>
        Attributes.TryGetValue(stat, out var value) ? value : null;

    public decimal? Haste => Attribute("Haste");

    public decimal? Expertise => Attribute("Expertise");
}
