using System.Globalization;

using Microsoft.Extensions.Primitives;

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

public sealed record TooltipQuery
{
    public string? Hero { get; init; }

    public string? Rarity { get; init; }

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

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "hero", "rarity", "itemLevel", "upgrades", "maxUpgrades", "noSocket", "potentialPower",
        "gemPowers", "armor", "trait", "allocatedTraits", "maxTraits",
    };

    private const int LevelCeiling = 10_000;

    private const decimal StatCeiling = 1_000_000_000_000m;

    public static TooltipQuery Parse(IQueryCollection query, StatParameters knownStats, out string? error)
    {
        error = null;

        var stats = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var gems = new List<GemPower>();
        var traits = new List<ImbuedTrait>();

        var byName = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in query) byName[key] = raw;

        var result = new TooltipQuery { NoSocket = byName.ContainsKey("noSocket") };

        foreach (var (key, raw) in byName)
        {
            if (!Reserved.Contains(key))
            {
                var stat = knownStats.Resolve(key);
                var percentage = false;

                if (stat is null &&
                    key.EndsWith(StatConversion.PercentageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    stat = knownStats.Resolve(key[..^StatConversion.PercentageSuffix.Length]);
                    percentage = stat is not null;
                }

                if (stat is null)
                {
                    error = $"Unknown parameter '{key}'.";
                    return result;
                }

                var attribute = knownStats.Row(stat);

                if (percentage && !StatConversion.Converts(attribute))
                {
                    error =
                        $"'{key[..^StatConversion.PercentageSuffix.Length]}' is not a rating, " +
                        $"so '{key}' has nothing to convert.";
                    return result;
                }

                var values = new List<decimal>(raw.Count);
                foreach (var value in raw)
                {
                    if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        error = $"Stat '{key}' has the non-numeric value '{value}'.";
                        return result;
                    }

                    if (Math.Abs(number) > StatCeiling)
                    {
                        error = $"Stat '{key}' is {value}, past the {StatCeiling:N0} a coefficient can be applied to.";
                        return result;
                    }

                    values.Add(number);
                }

                if (!percentage)
                {
                    stats[stat] = stats.TryGetValue(stat, out var already)
                        ? [.. already, .. values]
                        : values;
                }

                attributes[stat] = attributes.GetValueOrDefault(stat) + (percentage
                    ? StatConversion.FromPercentage(values.Sum(), attribute)
                    : StatConversion.FromRating(values.Sum(), attribute));
                continue;
            }

            if (key.Equals("gemPowers", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var value in raw)
                {
                    foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!GemPower.TryParse(part, CultureInfo.InvariantCulture, out var gem))
                        {
                            error = $"gemPowers entry '{part}' is not a gem type id and power separated by a colon.";
                            return result;
                        }

                        gems.Add(gem);
                    }
                }
            }

            if (key.Equals("trait", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var value in raw)
                {
                    foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!ImbuedTrait.TryParse(part, CultureInfo.InvariantCulture, out var trait))
                        {
                            error = $"trait entry '{part}' is not an item trait id, optionally followed by a colon " +
                                "and the rank it sits at.";
                            return result;
                        }

                        traits.Add(trait);
                    }
                }
            }
        }

        var text = (string name) => byName.TryGetValue(name, out var v) && v.Count > 0 ? v[^1] : null;

        if (!TryInt(text("itemLevel"), out var itemLevel, out error)) return result;
        if (!TryInt(text("upgrades"), out var upgrades, out error)) return result;
        if (!TryInt(text("maxUpgrades"), out var maxUpgrades, out error)) return result;
        if (!TryInt(text("allocatedTraits"), out var allocated, out error)) return result;
        if (!TryInt(text("maxTraits"), out var maxTraits, out error)) return result;

        if (itemLevel is < 0 or > LevelCeiling)
        {
            error = $"itemLevel {itemLevel} is outside 0 to {LevelCeiling:N0}.";
            return result;
        }

        if (!TryDecimal(text("potentialPower"), "potentialPower", out var potential, out error)) return result;
        if (!TryDecimal(text("armor"), "armor", out var armor, out error)) return result;

        return result with
        {
            Hero = Given(text("hero")),
            Rarity = Given(text("rarity")),
            ItemLevel = itemLevel,
            Upgrades = upgrades,
            MaxUpgrades = maxUpgrades,
            PotentialPower = potential,
            Armor = armor,
            Stats = stats,
            Attributes = attributes,
            GemPowers = gems,
            Traits = traits,
            AllocatedTraits = allocated ?? (traits.Count > 0 ? traits.Count : null),
            MaxTraits = maxTraits,
        };
    }

    private static string? Given(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool TryInt(string? value, out int? result, out string? error)
    {
        result = null;
        error = null;

        if (value is not { Length: > 0 }) return true;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"'{value}' is not a whole number.";
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryDecimal(string? value, string name, out decimal? result, out string? error)
    {
        result = null;
        error = null;

        if (value is not { Length: > 0 }) return true;

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"{name} '{value}' is not a number.";
            return false;
        }

        if (Math.Abs(parsed) > StatCeiling)
        {
            error = $"{name} is {value}, past the {StatCeiling:N0} this reads.";
            return false;
        }

        result = parsed;
        return true;
    }
}
