using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Services;

namespace Fellowship.Codex.Models;

public sealed record Facet(
    string Key,
    string Label,
    Func<EntityDocument, IEnumerable<string>> Values)
{
    public Func<IDesignTokenService, string, string?>? Colour { get; init; }

    public bool Primary { get; init; }

    public bool Matches(EntityDocument document, IReadOnlyCollection<string> wanted) =>
        Values(document).Any(value => wanted.Contains(value, StringComparer.OrdinalIgnoreCase));
}

public sealed record Flag(string Key, string Label, Func<EntityDocument, bool> Predicate);

public sealed record Measure(string Key, string Label, Func<EntityDocument, double?> Value)
{
    public string? Unit { get; init; }
}

public sealed record Sorting(string Key, string Label, Func<EntityDocument, IComparable?> Value);

public sealed class SortOrder : IComparer<IComparable?>
{
    public static readonly SortOrder Reading = new();

    public int Compare(IComparable? left, IComparable? right) => (left, right) switch
    {
        (null, null) => 0,
        (null, _) => 1,
        (_, null) => -1,
        (string first, string second) => string.Compare(first, second, StringComparison.OrdinalIgnoreCase),
        _ => left.CompareTo(right),
    };
}

public sealed record TypeView(
    EntityType Type, string Slug, string Label, Category Category, int Count, int Position);

internal static class DocumentFields
{
    private static readonly ConcurrentDictionary<(Type Document, string Property), PropertyInfo?> Cache = new();

    private static PropertyInfo? Property(EntityDocument document, string name) =>
        Cache.GetOrAdd((document.GetType(), name), key => key.Document.GetProperty(key.Property));

    public static double? Number(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) switch
        {
            null => null,
            double number => number,
            int number => number,
            var boxed => throw new InvalidOperationException(
                $"{document.GetType().Name}.{name} is {boxed.GetType().Name}, which DocumentFields.Number does not unbox."),
        };

    public static string? Text(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as string;

    public static IReadOnlyList<string> Words(EntityDocument document, string name) =>
        Property(document, name)?.GetValue(document) as IReadOnlyList<string> ?? [];
}

public static class FellowshipCodex
{
    public static IReadOnlyList<string> Schools(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.Schools ?? [],
        EffectDocument effect => effect.Schools ?? [],
        RelicPowerDocument relic => relic.Schools ?? [],
        WeaponPowerDocument weapon => weapon.Schools ?? [],
        _ => [],
    };

    public static string Spaced(string value)
    {
        var text = string.Join(" / ", value.Split('.'));
        var spaced = new System.Text.StringBuilder(text.Length + 8);

        for (var at = 0; at < text.Length; at++)
        {
            if (at > 0 && char.IsUpper(text[at]) && (char.IsLower(text[at - 1]) || char.IsDigit(text[at - 1])))
            {
                spaced.Append(' ');
            }

            spaced.Append(text[at]);
        }

        return spaced.ToString();
    }

    public static string Label(EntityType type) => PublicTypes.Label(type);

    public static string Number(double value) =>
        Math.Round((decimal)value, 3).Normalize().ToString(CultureInfo.InvariantCulture);

    public static string Magnitude(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
}
