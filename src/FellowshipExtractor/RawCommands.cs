using System.Text;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;

namespace FellowshipExtractor;

/// <summary>Verbatim access to container files. No mappings involved, so this always works.</summary>
public static class RawCommands
{
    public static async Task CatAsync(ExtractorOptions options, string filter, int maxLines)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        foreach (var file in Match(provider, filter))
        {
            Console.WriteLine($"===== {file.Path} ({file.Size} bytes)");
            var text = Encoding.UTF8.GetString(file.Read());
            foreach (var line in text.Split('\n').Take(maxLines))
            {
                Console.WriteLine(line.TrimEnd('\r'));
            }
        }
    }

    public static async Task ExtractAsync(ExtractorOptions options, string filter)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var root = Path.Combine(options.ResolvedOutputDir, "raw");
        var count = 0;
        var failures = new List<string>();

        foreach (var file in Match(provider, filter))
        {
            try
            {
                var target = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await File.WriteAllBytesAsync(target, file.Read());
                count++;
            }
            catch (Exception ex)
            {
                failures.Add($"{file.Path} ({file.Size} bytes): {ex.GetType().Name}: {ex.Message}");
            }
        }

        Console.WriteLine($"Extracted {count} file(s) matching '{filter}' to {root}");
        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"{failures.Count} file(s) failed to read:");
            foreach (var failure in failures) Console.Error.WriteLine($"  {failure}");
        }
    }

    private static IEnumerable<GameFile> Match(AbstractFileProvider provider, string filter) =>
        provider.Files.Values
            .Where(f => f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase);
}
