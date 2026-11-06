using System.IO.Enumeration;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

/// <summary>
/// The CSV files behind each <c>DataRegistry</c> asset. <c>AC_Hero_Gunde.csv</c> exists under
/// <c>RawDataTables/AbilityConstants</c> and in the Gunde plugin with values of its own, so a row bound through a
/// registry reads the copy that registry scans.
/// </summary>
public sealed class DataRegistries
{
    private const string RegistryClass = "DataRegistry";
    private const string RegistryTypeField = "RegistryType";
    private const string SearchRulesField = "SearchRules";
    private const string ScanPathsField = "AssetScanPaths";
    private const string IncludePatternsField = "IncludePatterns";
    private const string SearchContextField = "SourceSearchContext";
    private const string PluginContext = "ECSVSourceSearchContext::Plugin";
    private const string ContentRoot = "fellowship/Content/";
    private const string PluginsRoot = "fellowship/Plugins/";

    private sealed record Source(string Folder, IReadOnlyList<string> Patterns);

    private readonly Dictionary<string, List<Source>> _sources = new(StringComparer.Ordinal);

    public DataRegistries(AbstractFileProvider provider, IEnumerable<FAssetData> assets)
    {
        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(RegistryClass, StringComparison.Ordinal)) continue;
            if (!provider.TryLoadPackage(asset.PackageName.Text, out var package)) continue;

            var exports = package.GetExports().ToArray();

            if (exports.FirstOrDefault(export => export.ExportType == RegistryClass) is not { } registry) continue;
            if (RegistryType(registry) is not { } type) continue;

            var sources = exports.SelectMany(Sources).ToList();

            if (!_sources.TryGetValue(type, out var known)) _sources[type] = sources;
            else known.AddRange(sources);
        }
    }

    public bool Scans(string registry, string file) =>
        _sources.TryGetValue(registry, out var sources) && sources.Any(source => Matches(source, file));

    private static string? RegistryType(UObject registry) =>
        PropertyWalk.Get(registry, RegistryTypeField)?.GenericValue is FScriptStruct { StructType: IPropertyHolder type }
            ? PropertyWalk.GetString(type, "Name")
            : PropertyWalk.GetString(registry, RegistryTypeField);

    private static IEnumerable<Source> Sources(UObject export)
    {
        if (PropertyWalk.GetStruct(export, SearchRulesField) is not { } rules) yield break;

        var root = PropertyWalk.GetString(export, SearchContextField) == PluginContext ? PluginsRoot : ContentRoot;
        var patterns = Strings(rules, IncludePatternsField);

        foreach (var path in Strings(rules, ScanPathsField))
        {
            yield return new Source($"{root}{path.Replace('\\', '/').Trim('/')}/", patterns);
        }
    }

    private static IReadOnlyList<string> Strings(IPropertyHolder holder, string field) =>
        PropertyWalk.Get(holder, field)?.GenericValue is UScriptArray array
            ? [.. array.Properties.Select(element => PropertyWalk.AsString(element?.GenericValue)).OfType<string>()]
            : [];

    private static bool Matches(Source source, string file)
    {
        if (!file.StartsWith(source.Folder, StringComparison.OrdinalIgnoreCase)) return false;
        if (source.Patterns.Count == 0) return true;

        var name = Path.GetFileName(file);

        return source.Patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, name));
    }
}
