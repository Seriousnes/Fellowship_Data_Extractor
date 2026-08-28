using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class ResourcePools
{
    public const string Table = "CT_CharacterBaseAttributes";

    private const string Attribute = "CRHeroAttributeSet.MaxResources";

    private const string States =
        "the resource pool a hero fills, which is what a cost stated as a fraction of it is a fraction of";

    private readonly Dictionary<string, double> _byHero;

    public ResourcePools(AbstractFileProvider provider, HeroNames heroes, ConstantsTable constants)
    {
        var curves = CurveRows.Load(provider, Table, States);

        _byHero = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var hero in heroes.Profiles)
        {
            var row = $"{hero.Codename}.{Attribute}";

            if (constants.Lookup(row).Record?.Value is not { } pool)
            {
                if (Keys(curves, row) is not { } keys) continue;
                if (CurveRows.First(keys) is not { } filled) continue;

                pool = filled;
            }

            _byHero[hero.ShippedName] = pool;
        }

        if (_byHero.Count == 0)
        {
            throw new InvalidOperationException(
                $"No hero has a {Attribute} row in {Table} or in the constants tables, so {States} is unreadable " +
                "and every cost stated as a fraction would export as the fraction.");
        }
    }

    public int Count => _byHero.Count;

    public IReadOnlyCollection<string> Heroes => _byHero.Keys;

    public double? Of(string hero) => _byHero.TryGetValue(hero, out var pool) ? pool : null;

    private static FStructFallback? Keys(UCurveTable curves, string row) =>
        curves.RowMap.FirstOrDefault(entry => entry.Key.Text.Equals(row, StringComparison.OrdinalIgnoreCase)).Value;
}
