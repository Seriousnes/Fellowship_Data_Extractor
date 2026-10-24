using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed class HeroAttributes
{
    private const string HeroTablePrefix = "CT_Attributes_";

    private readonly Dictionary<string, SortedDictionary<string, double>> _byCodename =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byCodename.Count;

    public HeroAttributes(AbstractFileProvider provider, HeroNames heroes)
    {
        var tables = new List<UCurveTable>
        {
            CurveRows.Load(provider, CreatureStats.BaseTable, "the attributes a hero starts with"),
        };

        foreach (var file in provider.Files.Values)
        {
            if (!file.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Path.GetFileNameWithoutExtension(file.Path).StartsWith(HeroTablePrefix, StringComparison.OrdinalIgnoreCase)) continue;

            if (provider.LoadPackage(file).GetExports().OfType<UCurveTable>().FirstOrDefault() is { } table)
            {
                tables.Add(table);
            }
        }

        var codenames = heroes.Codenames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            foreach (var (rowName, row) in table.RowMap)
            {
                var segments = rowName.Text.Split('.');
                if (segments.Length != 3 || !codenames.Contains(segments[0])) continue;
                if (CurveRows.First(row) is not { } value) continue;

                if (!_byCodename.TryGetValue(segments[0], out var attributes))
                {
                    _byCodename[segments[0]] = attributes = new SortedDictionary<string, double>(StringComparer.Ordinal);
                }

                if (attributes.TryGetValue(segments[2], out var stated) && stated != value)
                {
                    throw new InvalidOperationException(
                        $"{segments[0]}'s {segments[2]} is stated as {stated} and as {value}, so the attribute " +
                        "the hero starts with is ambiguous.");
                }

                attributes[segments[2]] = value;
            }
        }

        if (codenames.FirstOrDefault(codename => !_byCodename.ContainsKey(codename)) is { } missing)
        {
            throw new InvalidOperationException(
                $"No attribute table has a {missing}.<AttributeSet>.<Attribute> row, so that hero would export " +
                "with no starting attributes.");
        }
    }

    public IReadOnlyDictionary<string, double>? For(string codename) => _byCodename.GetValueOrDefault(codename);

    /// <summary>The value every hero starts <paramref name="attribute"/> at, 0 where no hero's table states it, or null where heroes differ.</summary>
    public double? Shared(string attribute)
    {
        var values = _byCodename.Values.Select(attributes => attributes.GetValueOrDefault(attribute)).Distinct().ToArray();

        return values.Length == 1 ? values[0] : null;
    }
}
