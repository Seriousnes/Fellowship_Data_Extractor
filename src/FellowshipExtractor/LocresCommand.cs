using System.Text.Json;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor;

/// <summary>
/// Localization tables hold every display name, description and tooltip. They are plain
/// string data, so they parse with no mappings and give the searchable half of the database.
/// </summary>
public static class LocresCommand
{
    public static async Task RunAsync(ExtractorOptions options, string culture)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var path = $"fellowship/Content/Localization/Game/{culture}/Game.locres";

        if (!provider.Files.TryGetValue(path, out var file))
        {
            Console.Error.WriteLine($"Not found: {path}");
            return;
        }

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        var locres = new FTextLocalizationResource(archive);

        var target = Path.Combine(options.ResolvedOutputDir, $"locres-{culture}.jsonl");
        var count = 0;

        await using var writer = new StreamWriter(target);
        foreach (var (ns, entries) in locres.Entries)
        {
            foreach (var (key, entry) in entries)
            {
                var record = new
                {
                    ns = ns.Str,
                    key = key.Str,
                    text = entry.LocalizedString,
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(record));
                count++;
            }
        }

        Console.WriteLine($"Wrote {count} localized strings to {target}");
    }
}
