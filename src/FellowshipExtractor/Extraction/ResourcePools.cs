using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class ResourcePools
{
    public const string Table = "CT_CharacterBaseAttributes";

    public const string Primary = "Resources";

    public const string CeilingPrefix = "Max";

    private const string Attribute = "CRHeroAttributeSet.MaxResources";

    private const string States = "the resource pool a hero fills";

    private readonly Dictionary<string, Dictionary<string, double>> _byHero;

    private readonly SortedSet<string> _channels = new(StringComparer.Ordinal);

    private IReadOnlyCollection<string>? _universal;

    public ResourcePools(AbstractFileProvider provider, HeroNames heroes, ConstantsTable constants)
    {
        var curves = CurveRows.Load(provider, Table, States);
        var sets = Sets(curves);

        _byHero = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in heroes.Profiles)
        {
            var filled = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in Ceilings(constants, curves, hero.Codename))
            {
                if (Channel(row, hero.Codename, sets) is not { } channel) continue;

                _channels.Add(channel);

                if (Read(constants, curves, row) is not { } pool) continue;

                filled[channel] = pool;
            }

            if (filled.Count > 0) _byHero[hero.ShippedName] = filled;
        }

        if (!_byHero.Values.Any(filled => filled.ContainsKey(Primary)))
        {
            throw new InvalidOperationException(
                $"No hero has a {Attribute} row in {Table} or in the constants tables, so {States} is unreadable " +
                "and every cost stated as a fraction would export as the fraction.");
        }
    }

    public int Count => _byHero.Count;

    public IReadOnlyCollection<string> Heroes => _byHero.Keys;

    public IReadOnlyCollection<string> Channels => _channels;

    public IReadOnlyCollection<string> Universal =>
        _universal ??= [.. _channels.Where(channel => _byHero.Keys.All(hero => Ceiling(hero, channel) is not null))];

    public double? Of(string hero) => Stated(hero, Primary);

    public double? Ceiling(string hero, string channel) =>
        Stated(hero, channel) is { } pool && pool != 0 ? pool : null;

    public double? Captured(string hero, string attribute) =>
        attribute.StartsWith(CeilingPrefix, StringComparison.Ordinal)
            ? Ceiling(hero, attribute[CeilingPrefix.Length..])
            : null;

    private double? Stated(string hero, string channel) =>
        _byHero.TryGetValue(hero, out var filled) && filled.TryGetValue(channel, out var pool) ? pool : null;

    private static IEnumerable<string> Ceilings(ConstantsTable constants, UCurveTable curves, string codename)
    {
        var scope = codename + '.';
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in constants.AllUnder(codename))
        {
            if (seen.Add(record.Key)) yield return record.Key;
        }

        foreach (var row in curves.RowMap.Keys)
        {
            if (!row.Text.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) continue;
            if (seen.Add(row.Text)) yield return row.Text;
        }
    }

    private static HashSet<string> Sets(UCurveTable curves)
    {
        var sets = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in curves.RowMap.Keys)
        {
            if (row.Text.Split('.') is [_, { } set, _]) sets.Add(set);
        }

        return sets;
    }

    private static string? Channel(string row, string codename, IReadOnlySet<string> sets)
    {
        var segments = row.Split('.');
        if (segments.Length != 3) return null;
        if (!segments[0].Equals(codename, StringComparison.OrdinalIgnoreCase)) return null;
        if (!sets.Contains(segments[1])) return null;
        if (!segments[2].StartsWith(CeilingPrefix, StringComparison.Ordinal)) return null;

        var channel = segments[2][CeilingPrefix.Length..];

        return channel.Length == 0 ? null : channel;
    }

    private static double? Read(ConstantsTable constants, UCurveTable curves, string row)
    {
        if (constants.Lookup(row).Record?.Value is { } declared) return declared;
        if (Keys(curves, row) is not { } keys) return null;

        return CurveRows.First(keys);
    }

    private static FStructFallback? Keys(UCurveTable curves, string row) =>
        curves.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(row, StringComparison.OrdinalIgnoreCase)).Value;
}
