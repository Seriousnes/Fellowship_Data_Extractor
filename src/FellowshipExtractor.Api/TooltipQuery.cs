using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Api;

/// <summary>
/// One gem's contribution, as a caller spells it: the gem type's own id and the power the caller's
/// socketed gems of that type add up to. A type's bonus ladder is unlocked against that running
/// total, so a bag of gems reaches the tooltip as one number per type rather than as a list of gems.
/// </summary>
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

/// <summary>
/// The stat context a caller can hand a tooltip so its placeholders print numbers instead of
/// coefficients.
///
/// Three of the shapes a caller actually sends do not bind through a minimal API's default
/// parameter binding, which is why the whole query is read here rather than parameter by parameter.
///
/// A stat repeats. An item carries one slot per roll and the caller sends one value per slot, so
/// spirit=17&amp;spirit=9&amp;spirit=9 is an item with three spirit slots and not a malformed request.
/// Bound as a scalar the three values arrive joined as "17,9,9" and fail to parse.
///
/// A flag carries no value. noSocket is present or absent, and an empty string binds to neither
/// bool nor bool?, so presence is what has to be read.
///
/// A gem power is a pair. gemPowers=42:100 is one value holding two numbers.
/// </summary>
public sealed record TooltipQuery
{
    /// <summary>The hero the tooltip is being read as, under either their shipped name or codename.</summary>
    public string? Hero { get; init; }

    /// <summary>
    /// The rarity word the caller typed. Left as they typed it: which rung it names depends on
    /// whether the entity is a gem, and the entity is not known yet at binding time.
    /// </summary>
    public string? Rarity { get; init; }

    public int? ItemLevel { get; init; }
    public int? Upgrades { get; init; }
    public int? MaxUpgrades { get; init; }

    /// <summary>Set when the caller's copy of the item rolled without a gem socket.</summary>
    public bool NoSocket { get; init; }

    public decimal? PotentialPower { get; init; }

    /// <summary>
    /// The caller's stat values, one entry per stat name and one value per slot the caller holds
    /// of it, in the order they sent them, each the number the caller wrote.
    ///
    /// Kept in the caller's own scale, because these are slots: spirit=17 is a slot that rolled 17
    /// and an item's line prints +17, which is what the game prints on the gear. The attribute the
    /// 17 feeds is a different quantity and lives in <see cref="Attributes"/>, so neither reader
    /// has to undo the other's arithmetic.
    ///
    /// Keyed by the build's spelling rather than the caller's, because an item's slots, a token's
    /// attribute and the magnitude arithmetic are all keyed that way. The translation happens once,
    /// as the parameter is read.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<decimal>> Stats { get; init; } =
        new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the caller holds of each stat as the attribute it feeds, which for a rating is not the
    /// number they wrote: 50 haste rating is 8%, and lands here as 0.08.
    ///
    /// Both spellings arrive here, so a reader asking what the wearer holds does not have to know
    /// which one the caller used. haste=50 converts on the way in and hastePercentage=8 is taken as
    /// already converted, and the two say the same thing.
    ///
    /// One total per stat rather than a value per slot, because the diminishing returns are the
    /// character's and not the slot's: two 50-rating slots are 100 rating through one ladder, worth
    /// 15.86% rather than the 16% two separate conversions would give.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Attributes { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GemPower> GemPowers { get; init; } = [];

    /// <summary>A stat's total across its slots, which is what a coefficient multiplies.</summary>
    public decimal? Total(string stat) =>
        Stats.TryGetValue(stat, out var values) ? values.Sum() : null;

    /// <summary>What one stat is worth as the attribute it feeds, where the caller named it at all.</summary>
    public decimal? Attribute(string stat) =>
        Attributes.TryGetValue(stat, out var value) ? value : null;

    /// <summary>
    /// The wearer's haste as a fraction, where 0.3 is the +30% an ability advertises, matching the
    /// build's own constants: Hero.Spirit.Buff.HasteIncrease is 0.3.
    ///
    /// Read as the attribute rather than as the slots, because this is a fact about the wearer:
    /// haste=50 is 50 haste rating, which DT_AttributeProperties turns into 8%, and hastePercentage=8
    /// says the same thing for a caller reading the number off their own character sheet.
    ///
    /// Named here rather than spelled at each reader, because two of them ask and a stat this one
    /// is spelled wrong in would silently read as no haste at all.
    /// </summary>
    public decimal? Haste => Attribute("Haste");

    /// <summary>The names this type reads itself, so every other key is a stat or a mistake.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "hero", "rarity", "itemLevel", "upgrades", "maxUpgrades", "noSocket", "potentialPower",
        "gemPowers",
    };

    /// <summary>
    /// The item level above which the shipped stat curve stops being arithmetic anyone can do. The
    /// primary pool grows as 1.08^(level/15) and passes what a decimal can hold somewhere past
    /// twelve thousand, which used to reach the caller as a 500. Ten thousand is under that and
    /// thirty times the highest level the game has, so a request inside it always computes and a
    /// request outside it is told the bound rather than crashing on it.
    /// </summary>
    private const int LevelCeiling = 10_000;

    /// <summary>
    /// The largest stat a caller may claim to hold. A coefficient multiplies it, so the product has
    /// to stay inside a decimal too; a trillion is past any number the game produces and leaves
    /// fourteen digits of headroom for the largest shipped coefficient.
    /// </summary>
    private const decimal StatCeiling = 1_000_000_000_000m;

    /// <summary>
    /// Reads the whole query. A key that is neither reserved nor a stat the game names is rejected
    /// rather than ignored, because a caller who types spirt=17 would otherwise get a tooltip
    /// printed from an unstated stat of zero and no indication anything went wrong.
    /// </summary>
    public static TooltipQuery Parse(IQueryCollection query, StatParameters knownStats, out string? error)
    {
        error = null;

        var stats = new Dictionary<string, IReadOnlyList<decimal>>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var gems = new List<GemPower>();

        // A query string is matched case sensitively by IQueryCollection and case insensitively by
        // everything below, so the whole query is re-keyed once here. Without it a caller writing
        // ItemLevel=315 has their key recognised as reserved and then read back as absent, and the
        // tooltip prints as though they had never sent it.
        var byName = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, raw) in query) byName[key] = raw;

        var result = new TooltipQuery { NoSocket = byName.ContainsKey("noSocket") };

        foreach (var (key, raw) in byName)
        {
            if (!Reserved.Contains(key))
            {
                // A stat named plainly is the rating on the gear. The same stat with Percentage
                // after it is the caller saying they hold the converted number already, which is
                // what a player reads off their own character sheet. The plain spelling is tried
                // first, so a printed name that ends in the word would still be read as itself.
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

                // A stat the build converts nothing for has no percentage to be given: Strength is
                // the number it says, and accepting strengthPercentage would quietly answer a
                // question the build does not ask. Named back to the caller in their own spelling,
                // because the build's is not an address this accepts.
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

                // The plain form is a slot, so it is kept as the caller wrote it and the item's
                // line prints that number. The percentage form is not a slot at all: it states what
                // the wearer's character sheet reads, so it says nothing about what this item
                // rolled and leaves the item's own slots to be computed.
                if (!percentage)
                {
                    stats[stat] = stats.TryGetValue(stat, out var already)
                        ? [.. already, .. values]
                        : values;
                }

                // What the whole of it is worth as the attribute it feeds. Converted from the total
                // rather than slot by slot, because a rating's diminishing returns are taken once
                // against everything the character holds.
                attributes[stat] = attributes.GetValueOrDefault(stat) + (percentage
                    ? StatConversion.FromPercentage(values.Sum(), attribute)
                    : StatConversion.FromRating(values.Sum(), attribute));
                continue;
            }

            if (key.Equals("gemPowers", StringComparison.OrdinalIgnoreCase))
            {
                // Either repeated or comma separated, because a caller writing a URL by hand does
                // one and a caller building it from a list does the other.
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
        }

        var text = (string name) => byName.TryGetValue(name, out var v) && v.Count > 0 ? v[^1] : null;

        if (!TryInt(text("itemLevel"), out var itemLevel, out error)) return result;
        if (!TryInt(text("upgrades"), out var upgrades, out error)) return result;
        if (!TryInt(text("maxUpgrades"), out var maxUpgrades, out error)) return result;

        if (itemLevel is < 0 or > LevelCeiling)
        {
            error = $"itemLevel {itemLevel} is outside 0 to {LevelCeiling:N0}.";
            return result;
        }

        if (text("potentialPower") is { Length: > 0 } potential)
        {
            if (!decimal.TryParse(potential, NumberStyles.Number, CultureInfo.InvariantCulture, out var power))
            {
                error = $"potentialPower '{potential}' is not a number.";
                return result;
            }

            if (Math.Abs(power) > StatCeiling)
            {
                error = $"potentialPower is {potential}, past the {StatCeiling:N0} this reads.";
                return result;
            }
        }

        return result with
        {
            // An empty parameter is the same as an absent one. A caller building a URL from a form
            // sends hero= for "no hero chosen", and reading that as a hero named nothing answers
            // "no reading for " with the name left off the end of the sentence.
            Hero = Given(text("hero")),
            Rarity = Given(text("rarity")),
            ItemLevel = itemLevel,
            Upgrades = upgrades,
            MaxUpgrades = maxUpgrades,
            PotentialPower = text("potentialPower") is { Length: > 0 } p
                ? decimal.Parse(p, NumberStyles.Number, CultureInfo.InvariantCulture)
                : null,
            Stats = stats,
            Attributes = attributes,
            GemPowers = gems,
        };
    }

    /// <summary>A parameter the caller actually gave, where an empty one counts as absent.</summary>
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
}
