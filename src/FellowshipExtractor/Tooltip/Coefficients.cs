namespace FellowshipExtractor.Tooltip;

/// <summary>The row a token's number came from, the attribute it multiplies, and the number.</summary>
public readonly record struct ResolvedCoefficient(string Key, string? Attribute, double Value);

/// <summary>
/// Finding the one number a description token multiplies a stat by.
///
/// A tuning row that scales off an attribute says so in its own name: AoeDamageIntellectCoefficient,
/// DamageStrengthCoefficient, AgilityCoefficient. That is the designer's own label rather than
/// anything inferred, and it names the attribute as well as marking the row, so both halves of
/// "5.8482 x Intellect" come from one match.
///
/// The marker has to sit in the row's last segment, and a row ending exactly in Coefficient
/// outranks one carrying a qualifier after it. Fire Ball carries both AoeDamageIntellectCoefficient
/// and DotIntellectCoefficientPerTick, and only the first is the direct hit that {FireBallDirect}
/// prints; ranking them tells the two apart while still letting an effect whose only coefficient is
/// qualified be read, which is how Fluttercall's 1.815 per tick and Double Strike's 0.825 per hit
/// arrive.
/// </summary>
public static class Coefficients
{
    private const string Marker = "Coefficient";

    /// <summary>
    /// The qualifiers a coefficient row may carry after the marker, from the census of all 1,223
    /// rows whose last segment contains it: 1,187 carry none, then PerTick x25, PerHit x3,
    /// PerPulse x2, PerStack x2, PerSpeed x1 and Party x1. Each of these names the occurrence the
    /// coefficient applies to and leaves the row a coefficient of an attribute.
    ///
    /// The two the census also turns up and this list leaves out - CoefficientScaler x1 and
    /// CoefficientMultiplierPerStack x1 - scale a coefficient rather than being one.
    /// DoTHotCoefficientScaler = 1.25 is reachable from several Vigor abilities and would answer
    /// for their damage tokens as 125% of nothing nameable.
    /// </summary>
    private static readonly string[] Qualifiers =
        ["PerTick", "PerHit", "PerPulse", "PerStack", "PerSpeed", "Party"];

    /// <summary>
    /// The row, attribute and coefficient a token resolves to, or null when the rows do not
    /// identify exactly one. Several candidates means the description alone cannot say which is
    /// meant - Temporal Barrage prints a heal and a damage number from two rows that differ only in
    /// that word - so nothing is reported rather than the wrong one.
    ///
    /// A row another token in the same description already accounts for is one of those rivals and
    /// is never the answer. Stone Shield names its stone health and its stone damage as two Agility
    /// coefficients and Incinerate's channel interval sits beside two coefficients both spoken for;
    /// counting the taken rows keeps either from being printed as the other.
    ///
    /// The attribute may come back null. A row whose prefix names no attribute the game has is not
    /// evidence for one, and the reader would rather print a bare share than invent MaxDamage.
    /// </summary>
    public static ResolvedCoefficient? From(
        IReadOnlyDictionary<string, object?>? constants,
        IReadOnlySet<string>? accountedFor = null,
        IReadOnlyList<string>? scalesOff = null)
    {
        if (constants is null) return null;

        return Only(constants, exact: true, accountedFor, scalesOff)
            ?? Only(constants, exact: false, accountedFor, scalesOff);
    }

    /// <summary>
    /// The one row in a tier, or null when none or several are. The two tiers are asked separately
    /// rather than as one list, because a row ending exactly in Coefficient is the direct number and
    /// a qualified row beside it would otherwise read as a rival candidate and silence both.
    /// </summary>
    private static ResolvedCoefficient? Only(
        IReadOnlyDictionary<string, object?> constants,
        bool exact,
        IReadOnlySet<string>? accountedFor,
        IReadOnlyList<string>? scalesOff)
    {
        ResolvedCoefficient? found = null;
        var candidates = 0;

        foreach (var (key, value) in constants)
        {
            if (value is not double number) continue;

            var leaf = key[(key.LastIndexOf('.') + 1)..];
            var at = leaf.IndexOf(Marker, StringComparison.Ordinal);

            // The prefix is what names the attribute, so a leaf that is nothing but the marker and
            // a qualifier names none and is not a candidate.
            if (at <= 0) continue;

            var qualifier = leaf[(at + Marker.Length)..];
            if (exact != (qualifier.Length == 0)) continue;
            if (!exact && !Qualifiers.Contains(qualifier, StringComparer.Ordinal)) continue;

            if (++candidates > 1) return null;
            if (accountedFor?.Contains(key) == true) continue;

            found = new ResolvedCoefficient(
                key, NamedByShippedText.GetValueOrDefault(leaf) ?? Attribute(leaf[..at], scalesOff), number);
        }

        return found;
    }

    /// <summary>
    /// Rows whose own name is silent about the attribute and whose shipped text is not. Keyed on
    /// the whole leaf rather than on the prefix, so it cannot catch a row that merely starts the
    /// same way, and read before the prefix rule because the prefix has nothing to say.
    ///
    /// One row in the corpus qualifies. MaxDamageCoefficient = 42.5 is Voidbringer's Touch's
    /// accumulation cap; neither its ability nor its token captures any attribute, and the effect's
    /// own shipped description supplies the missing object: locres entry
    /// 46FD4C5042D9A88A94664A9B1846144C reads "or when the accumulated damage reaches 4250% of the
    /// source's primary attribute". MaxDamageCoefficient occurs exactly once across all 13,156
    /// tuning rows, so the leaf identifies that row on its own.
    /// </summary>
    private static readonly Dictionary<string, string> NamedByShippedText =
        new(StringComparer.Ordinal) { ["MaxDamageCoefficient"] = "PrimaryAttribute" };

    /// <summary>
    /// The attribute a coefficient row names. The prefix is the designer's own label and its tail is
    /// the attribute: DamageStrength -> Strength, Agility -> Agility.
    ///
    /// 173 of the 1,187 exact rows have a prefix whose tail is no attribute the game has. 126 of
    /// them say PrimaryAttribute, which is the game's own name for the stat a hero's class decides,
    /// and reads as itself. For the rest the row is silent, and the token's own captured attributes
    /// are asked instead: IntellectDamageCoefficient on Soulbrand and Dawnflare sits under a
    /// placeholder capturing DamageScale and Intellect, and Intellect is the only attribute there.
    ///
    /// Where neither settles it nothing is claimed. Heart Splitter's DotCoefficient is the share
    /// its own sentence spends on the Total Rend Damage it already names, and calling that a share
    /// of Dot would be an invention.
    /// </summary>
    private static string? Attribute(string prefix, IReadOnlyList<string>? scalesOff)
    {
        foreach (var (row, named) in Attributes)
        {
            if (prefix.EndsWith(row, StringComparison.Ordinal)) return named;
        }

        string? only = null;
        foreach (var captured in scalesOff ?? [])
        {
            if (!Known.Contains(captured)) continue;
            if (only is not null && only != captured) return null;
            only = captured;
        }

        return only;
    }

    /// <summary>
    /// The attributes a coefficient row can name. Taken from the names the effects themselves
    /// capture, so a row reading DamageStrengthCoefficient reports Strength rather than
    /// DamageStrength.
    ///
    /// Two of the 13,156 shipped constant rows are abbreviated: 67 rows name the attribute Int and
    /// one names it Str, and no other short spelling exists in the corpus. They come last because
    /// the rule takes the first spelling the name ends in, and the written-out forms are the ones a
    /// row means when it carries both.
    /// </summary>
    private static readonly (string Row, string Named)[] Attributes =
    [
        ("PrimaryAttribute", "PrimaryAttribute"),
        ("Intellect", "Intellect"), ("Strength", "Strength"), ("Agility", "Agility"),
        ("Stamina", "Stamina"), ("Spirit", "Spirit"), ("MaxHealth", "MaxHealth"),
        ("Health", "Health"), ("AttackPower", "AttackPower"), ("CritChance", "CritChance"),
        ("Haste", "Haste"), ("Expertise", "Expertise"), ("Armor", "Armor"),
        ("Int", "Intellect"), ("Str", "Strength"),
    ];

    private static readonly HashSet<string> Known =
        new(Attributes.Select(a => a.Named), StringComparer.Ordinal);
}
