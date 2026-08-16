using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Extraction;

public sealed record LocalizedString(string Namespace, string Key, string Text);

public sealed class LocalizedText
{
    public const string DefaultCulture = "en";

    private readonly Dictionary<string, string> _byKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> _contested = new(StringComparer.Ordinal);

    public string Culture { get; }

    public int Count { get; }

    public IReadOnlyCollection<string> Contested => _contested;

    public static string PathFor(string culture) =>
        $"fellowship/Content/Localization/Game/{culture}/Game.locres";

    public LocalizedText(AbstractFileProvider provider, string culture = DefaultCulture)
    {
        Culture = culture;

        foreach (var entry in Read(provider, culture))
        {
            Count++;

            if (!_byKey.TryAdd(entry.Key, entry.Text) &&
                !_byKey[entry.Key].Equals(entry.Text, StringComparison.Ordinal))
            {
                _contested.Add(entry.Key);
            }
        }
    }

    public static IEnumerable<LocalizedString> Read(AbstractFileProvider provider, string culture)
    {
        if (!provider.Files.TryGetValue(PathFor(culture), out var file)) yield break;

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        var locres = new FTextLocalizationResource(archive);

        foreach (var (ns, entries) in locres.Entries)
        {
            foreach (var (key, entry) in entries)
            {
                if (entry.LocalizedString is not { } text) continue;

                yield return new LocalizedString(ns.Str, key.Str, text);
            }
        }
    }

    public string? Find(string key) =>
        !_contested.Contains(key) && _byKey.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
}
