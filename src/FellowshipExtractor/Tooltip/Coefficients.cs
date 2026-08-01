namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Finding the one number a description token multiplies a stat by.
///
/// A tuning row that scales off an attribute says so in its own name: AoeDamageIntellectCoefficient,
/// DamageStrengthCoefficient, AgilityCoefficient. That is the designer's own label rather than
/// anything inferred, and it names the attribute as well as marking the row, so both halves of
/// "5.8482 x Intellect" come from one match.
///
/// The suffix has to be the end of the row's last segment. Fire Ball carries both
/// AoeDamageIntellectCoefficient and DotIntellectCoefficientPerTick, and only the first is the
/// direct hit that {FireBallDirect} prints; requiring the row to end there tells them apart.
/// </summary>
public static class Coefficients
{
    private const string Suffix = "Coefficient";

    /// <summary>
    /// The attribute and coefficient a token resolves to, or null when the rows do not identify
    /// exactly one. Several candidates means the description alone cannot say which is meant -
    /// Temporal Barrage prints a heal and a damage number from two rows that differ only in that
    /// word - so nothing is reported rather than the wrong one.
    /// </summary>
    public static (string Attribute, double Value)? From(IReadOnlyDictionary<string, object?>? constants)
    {
        if (constants is null) return null;

        (string Attribute, double Value)? found = null;

        foreach (var (key, value) in constants)
        {
            if (value is not double number) continue;

            var leaf = key.Split('.')[^1];
            if (!leaf.EndsWith(Suffix, StringComparison.Ordinal)) continue;

            var attribute = leaf[..^Suffix.Length];
            if (attribute.Length == 0) continue;

            // The attribute is the tail of the name: DamageStrength -> Strength, Agility -> Agility.
            attribute = Attributes.FirstOrDefault(a => attribute.EndsWith(a, StringComparison.Ordinal))
                ?? attribute;

            if (found is not null) return null;
            found = (attribute, number);
        }

        return found;
    }

    /// <summary>
    /// The attributes a coefficient row can name. Taken from the names the effects themselves
    /// capture, so a row reading DamageStrengthCoefficient reports Strength rather than
    /// DamageStrength.
    /// </summary>
    private static readonly string[] Attributes =
    [
        "Intellect", "Strength", "Agility", "Stamina", "Spirit", "MaxHealth", "Health",
        "AttackPower", "CritChance", "Haste", "Expertise", "Armor",
    ];
}
