using System.Globalization;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class DynamicTextRows
{
    public const string RowStructure = "/Script/fellowship.CRDynamicTextDataRow";

    private const string TablePrefix = "DT_DynamicText_Hero_";
    private const string SentenceHolder = "DynamicText";
    private const string SentenceField = "Text";
    private const string TokensField = "Tokens";
    private const string TokenNameField = "TokenName";
    private const string PercentField = "ShowPercent";
    private const string MagnitudeProperty = "Magnitude.ScalableFloatMagnitude";

    private readonly Dictionary<string, SortedDictionary<string, string>> _byRowKey =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, int> _literal = new(StringComparer.Ordinal);

    public int RowCount => _byRowKey.Count;

    public int SentenceCount => _byRowKey.Values.Sum(byHero => byHero.Count);

    public int Substituted { get; private set; }

    public IReadOnlyDictionary<string, int> Literal => _literal;

    public List<string> Unresolved { get; } = [];

    public DynamicTextRows(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ConstantsTable constants,
        HeroNames heroes)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            var assetName = AbilityPresentationIndex.AssetName(objectPath);

            if (!assetName.StartsWith(TablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                Unresolved.Add(assetName);
                continue;
            }

            var suffix = assetName[TablePrefix.Length..];
            var codename = heroes.Profile(suffix)?.Codename;

            if (codename is null)
            {
                Unresolved.Add(assetName);
                continue;
            }

            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (key, row) in table.RowMap)
            {
                if (PropertyWalk.GetStruct(row, SentenceHolder) is not { } holder) continue;
                if (PropertyWalk.GetText(holder, SentenceField) is not { Length: > 0 } sentence) continue;

                if (!_byRowKey.TryGetValue(key.Text, out var byHero))
                {
                    _byRowKey[key.Text] = byHero = new SortedDictionary<string, string>(StringComparer.Ordinal);
                }

                byHero[codename] = Substitute(sentence, Declared(holder), constants);
            }
        }

        if (_byRowKey.Count == 0)
        {
            throw new InvalidOperationException(
                $"No {TablePrefix}<codename> table declares {RowStructure}, so every perk whose description is the " +
                "shared hero token exports that token and nothing a hero can read.");
        }
    }

    public IReadOnlyDictionary<string, string>? ForTag(string tagName)
    {
        var stem = Stem(tagName);

        var matched = _byRowKey.Keys
            .Where(key => Shares(stem, key))
            .OrderByDescending(key => key.Length)
            .ToArray();

        if (matched.Length == 0) return null;
        if (matched.Length > 1 && matched[0].Length == matched[1].Length) return null;

        return _byRowKey[matched[0]];
    }

    private static bool Shares(string stem, string rowKey)
    {
        var (shorter, longer) = stem.Length <= rowKey.Length ? (stem, rowKey) : (rowKey, stem);

        if (!longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase)) return false;
        if (longer.Length == shorter.Length) return true;

        var next = longer[shorter.Length];
        return next == '_' || char.IsUpper(next);
    }

    private static string Stem(string tagName)
    {
        var underscore = tagName.IndexOf('_');
        return underscore < 0 ? tagName : tagName[(underscore + 1)..];
    }

    private static Dictionary<string, IPropertyHolder> Declared(IPropertyHolder holder)
    {
        var tokens = new Dictionary<string, IPropertyHolder>(StringComparer.OrdinalIgnoreCase);

        if (PropertyWalk.Get(holder, TokensField)?.GenericValue is not UScriptArray declared) return tokens;

        foreach (var entry in declared.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder token }) continue;
            if (PropertyWalk.GetString(token, TokenNameField) is not { } name) continue;

            tokens.TryAdd(name, token);
        }

        return tokens;
    }

    private string Substitute(
        string sentence, IReadOnlyDictionary<string, IPropertyHolder> tokens, ConstantsTable constants)
    {
        foreach (var written in DescriptionTokens.Tokens(sentence).Distinct(StringComparer.Ordinal))
        {
            if (tokens.TryGetValue(written, out var token) && Stated(token, constants) is { } printed)
            {
                sentence = sentence.Replace($"{{{written}}}", printed, StringComparison.Ordinal);
                Substituted++;
                continue;
            }

            _literal[written] = _literal.GetValueOrDefault(written) + 1;
        }

        return sentence;
    }

    private static string? Stated(IPropertyHolder token, ConstantsTable constants)
    {
        var magnitude = CurveBindings.Collect(token, constants)
            .FirstOrDefault(binding => binding.Property.Equals(MagnitudeProperty, StringComparison.Ordinal));

        if (magnitude?.Value is not { } value) return null;

        return PropertyWalk.Get(token, PercentField)?.GenericValue is true
            ? Printed(value * 100) + "%"
            : Printed(value);
    }

    private static string Printed(double value) =>
        Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);
}
