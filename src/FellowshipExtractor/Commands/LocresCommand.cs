using System.Text.Json;

using FellowshipExtractor.Tooltip;

namespace FellowshipExtractor.Commands;

public static class LocresCommand
{
    public static async Task RunAsync(ExtractorOptions options, string culture)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        if (!provider.Files.ContainsKey(LocalizedText.PathFor(culture)))
        {
            Console.Error.WriteLine($"Not found: {LocalizedText.PathFor(culture)}");
            return;
        }

        var target = Path.Combine(options.ResolvedOutputDir, $"locres-{culture}.jsonl");
        var count = 0;

        await using var writer = new StreamWriter(target);
        foreach (var entry in LocalizedText.Read(provider, culture))
        {
            var record = new
            {
                ns = entry.Namespace,
                key = entry.Key,
                text = entry.Text,
            };
            await writer.WriteLineAsync(JsonSerializer.Serialize(record));
            count++;
        }

        Console.WriteLine($"Wrote {count} localized strings to {target}");
    }
}
