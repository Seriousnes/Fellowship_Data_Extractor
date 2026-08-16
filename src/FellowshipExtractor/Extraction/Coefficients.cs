namespace FellowshipExtractor.Extraction;

public readonly record struct ResolvedCoefficient(string Key, string? Attribute, double Value);

public readonly record struct NamedCoefficient(string Key, string Attribute, string? Part, double Value);

public static class Coefficients
{
    private const string Marker = "Coefficient";

    public static IReadOnlyList<NamedCoefficient> All(
        IReadOnlyDictionary<string, object?> constants,
        IReadOnlySet<string> own,
        IReadOnlySet<string> accountedFor)
    {
        var found = new List<NamedCoefficient>();

        foreach (var (key, value) in constants)
        {
            if (value is not double number || number == 0) continue;
            if (!own.Contains(key) || accountedFor.Contains(key)) continue;

            var leaf = key[(key.LastIndexOf('.') + 1)..];
            if (!leaf.EndsWith(Marker, StringComparison.Ordinal) || leaf.Length == Marker.Length) continue;

            var prefix = leaf[..^Marker.Length];

            if (Attributed(prefix) is not { } named) continue;

            var path = key.Length > leaf.Length ? key[..^(leaf.Length + 1)] : null;

            var part = (path, named.Qualifier) switch
            {
                (null, "") => null,
                (null, { } qualifier) => qualifier,
                ({ } under, "") => under,
                ({ } under, { } qualifier) => $"{under}.{qualifier}",
            };

            found.Add(new NamedCoefficient(key, named.Attribute, part, number));
        }

        found.Sort((left, right) =>
            string.CompareOrdinal(left.Part ?? string.Empty, right.Part ?? string.Empty) is var order && order != 0
                ? order
                : string.CompareOrdinal(left.Attribute, right.Attribute));

        return found;
    }

    private static (string Attribute, string Qualifier)? Attributed(string prefix)
    {
        if (NamedByShippedText.GetValueOrDefault(prefix + Marker) is { } shipped) return (shipped, string.Empty);

        foreach (var (row, named) in Attributes)
        {
            if (prefix.EndsWith(row, StringComparison.Ordinal)) return (named, prefix[..^row.Length]);
        }

        return null;
    }

    private static readonly string[] Qualifiers =
        ["PerTick", "PerHit", "PerPulse", "PerStack", "PerSpeed", "Party"];

    public static ResolvedCoefficient? From(
        IReadOnlyDictionary<string, object?>? constants,
        IReadOnlySet<string>? accountedFor = null,
        IReadOnlyList<string>? scalesOff = null)
    {
        if (constants is null) return null;

        return Only(constants, exact: true, accountedFor, scalesOff)
            ?? Only(constants, exact: false, accountedFor, scalesOff);
    }

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

    private static readonly Dictionary<string, string> NamedByShippedText =
        new(StringComparer.Ordinal) { ["MaxDamageCoefficient"] = "PrimaryAttribute" };

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

    private static readonly string[] Primary = ["Intellect", "Strength", "Agility"];

    public static string Captured(string named, IReadOnlyList<string>? capturedAttributes)
    {
        if (!Primary.Contains(named, StringComparer.Ordinal)) return named;

        var only = capturedAttributes?.Where(a => Primary.Contains(a, StringComparer.Ordinal)).ToArray() ?? [];

        return only.Length == 1 ? only[0] : named;
    }
}
